package com.mrhaynes.routeplanner

import android.content.Context
import android.location.Address
import android.location.Geocoder
import android.os.Build
import androidx.annotation.RequiresApi
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext
import java.util.Locale
import kotlin.coroutines.resume

/** Turns addresses into coordinates using the phone's built-in Google geocoder. */
class Geocoding(context: Context) {
    private val geocoder = Geocoder(context, Locale.getDefault())

    data class Result(val label: String, val point: GeoPoint)

    val isAvailable: Boolean get() = Geocoder.isPresent()

    /**
     * Looks up [query]. When [near] is given, matches around that point are
     * tried first (helps addresses with no city, like "123 Main St").
     */
    suspend fun search(query: String, near: GeoPoint? = null): Result? {
        parseLatLng(query)?.let { return Result(query.trim(), it) }
        if (!isAvailable) return null
        val address = near?.let { point -> lookup { geocoder, callback -> fromName(geocoder, query, point, callback) } }
            ?: lookup { geocoder, callback -> fromName(geocoder, query, null, callback) }
            ?: return null
        val label = address.getAddressLine(0) ?: query.trim()
        return Result(label, GeoPoint(address.latitude, address.longitude))
    }

    suspend fun describe(point: GeoPoint): String {
        val fallback = String.format(Locale.US, "%.5f, %.5f", point.lat, point.lng)
        if (!isAvailable) return fallback
        val address = lookup { geocoder, callback -> fromLocation(geocoder, point, callback) }
        return address?.getAddressLine(0) ?: fallback
    }

    private suspend fun lookup(
        call: (Geocoder, (List<Address>?) -> Unit) -> Unit,
    ): Address? = if (Build.VERSION.SDK_INT >= 33) {
        suspendCancellableCoroutine { cont ->
            try {
                call(geocoder) { list -> if (cont.isActive) cont.resume(list?.firstOrNull()) }
            } catch (e: Exception) {
                if (cont.isActive) cont.resume(null)
            }
        }
    } else {
        withContext(Dispatchers.IO) {
            var result: Address? = null
            call(geocoder) { list -> result = list?.firstOrNull() }
            result
        }
    }

    @Suppress("DEPRECATION")
    private fun fromName(g: Geocoder, query: String, near: GeoPoint?, callback: (List<Address>?) -> Unit) {
        if (near == null) {
            if (Build.VERSION.SDK_INT >= 33) {
                g.getFromLocationName(query, 1, listener(callback))
            } else {
                callback(runCatching { g.getFromLocationName(query, 1) }.getOrNull())
            }
            return
        }
        // Roughly 80 km around the point.
        val south = (near.lat - NEAR_DEGREES).coerceAtLeast(-90.0)
        val north = (near.lat + NEAR_DEGREES).coerceAtMost(90.0)
        val west = (near.lng - NEAR_DEGREES).coerceAtLeast(-180.0)
        val east = (near.lng + NEAR_DEGREES).coerceAtMost(180.0)
        if (Build.VERSION.SDK_INT >= 33) {
            g.getFromLocationName(query, 1, south, west, north, east, listener(callback))
        } else {
            callback(runCatching { g.getFromLocationName(query, 1, south, west, north, east) }.getOrNull())
        }
    }

    @Suppress("DEPRECATION")
    private fun fromLocation(g: Geocoder, p: GeoPoint, callback: (List<Address>?) -> Unit) {
        if (Build.VERSION.SDK_INT >= 33) {
            g.getFromLocation(p.lat, p.lng, 1, listener(callback))
        } else {
            callback(runCatching { g.getFromLocation(p.lat, p.lng, 1) }.getOrNull())
        }
    }

    @RequiresApi(33)
    private fun listener(callback: (List<Address>?) -> Unit) =
        object : Geocoder.GeocodeListener {
            override fun onGeocode(addresses: MutableList<Address>) = callback(addresses)
            override fun onError(errorMessage: String?) = callback(null)
        }

    companion object {
        private const val NEAR_DEGREES = 0.75
        private val LAT_LNG = Regex("""^\s*(-?\d{1,2}(?:\.\d+)?)\s*,\s*(-?\d{1,3}(?:\.\d+)?)\s*$""")

        /** Accepts input like "40.7128, -74.0060". */
        fun parseLatLng(text: String): GeoPoint? {
            val match = LAT_LNG.matchEntire(text) ?: return null
            val lat = match.groupValues[1].toDouble()
            val lng = match.groupValues[2].toDouble()
            if (lat !in -90.0..90.0 || lng !in -180.0..180.0) return null
            return GeoPoint(lat, lng)
        }
    }
}
