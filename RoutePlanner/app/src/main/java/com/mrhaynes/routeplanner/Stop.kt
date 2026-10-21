package com.mrhaynes.routeplanner

import android.content.Context
import org.json.JSONArray
import org.json.JSONObject

data class Stop(
    val id: Long,
    val label: String,
    val lat: Double,
    val lng: Double,
    val done: Boolean = false,
) {
    val point get() = GeoPoint(lat, lng)
}

/** The address the route starts and finishes at. */
data class Home(val label: String, val lat: Double, val lng: Double) {
    val point get() = GeoPoint(lat, lng)
}

/** Saves the stop list and settings so they survive the app being closed. */
class StopStore(context: Context) {
    private val prefs = context.getSharedPreferences("route_planner", Context.MODE_PRIVATE)

    fun load(): MutableList<Stop> {
        val raw = prefs.getString(KEY_STOPS, null) ?: return mutableListOf()
        return try {
            val array = JSONArray(raw)
            MutableList(array.length()) { i ->
                val o = array.getJSONObject(i)
                Stop(
                    id = o.getLong("id"),
                    label = o.getString("label"),
                    lat = o.getDouble("lat"),
                    lng = o.getDouble("lng"),
                    done = o.optBoolean("done", false),
                )
            }
        } catch (e: Exception) {
            mutableListOf()
        }
    }

    fun save(stops: List<Stop>) {
        val array = JSONArray()
        stops.forEach {
            array.put(
                JSONObject()
                    .put("id", it.id)
                    .put("label", it.label)
                    .put("lat", it.lat)
                    .put("lng", it.lng)
                    .put("done", it.done),
            )
        }
        prefs.edit().putString(KEY_STOPS, array.toString()).apply()
    }

    var startFromLocation: Boolean
        get() = prefs.getBoolean(KEY_START_LOCATION, true)
        set(value) = prefs.edit().putBoolean(KEY_START_LOCATION, value).apply()

    var roundTrip: Boolean
        get() = prefs.getBoolean(KEY_ROUND_TRIP, false)
        set(value) = prefs.edit().putBoolean(KEY_ROUND_TRIP, value).apply()

    var useHome: Boolean
        get() = prefs.getBoolean(KEY_USE_HOME, false)
        set(value) = prefs.edit().putBoolean(KEY_USE_HOME, value).apply()

    var farthestFirst: Boolean
        get() = prefs.getBoolean(KEY_FARTHEST_FIRST, false)
        set(value) = prefs.edit().putBoolean(KEY_FARTHEST_FIRST, value).apply()

    var home: Home?
        get() {
            val label = prefs.getString(KEY_HOME_LABEL, null) ?: return null
            val lat = prefs.getString(KEY_HOME_LAT, null)?.toDoubleOrNull() ?: return null
            val lng = prefs.getString(KEY_HOME_LNG, null)?.toDoubleOrNull() ?: return null
            return Home(label, lat, lng)
        }
        set(value) {
            val edit = prefs.edit()
            if (value == null) {
                edit.remove(KEY_HOME_LABEL).remove(KEY_HOME_LAT).remove(KEY_HOME_LNG)
            } else {
                edit.putString(KEY_HOME_LABEL, value.label)
                    .putString(KEY_HOME_LAT, value.lat.toString())
                    .putString(KEY_HOME_LNG, value.lng.toString())
            }
            edit.apply()
        }

    private companion object {
        const val KEY_STOPS = "stops"
        const val KEY_START_LOCATION = "start_from_location"
        const val KEY_ROUND_TRIP = "round_trip"
        const val KEY_USE_HOME = "use_home"
        const val KEY_FARTHEST_FIRST = "farthest_first"
        const val KEY_HOME_LABEL = "home_label"
        const val KEY_HOME_LAT = "home_lat"
        const val KEY_HOME_LNG = "home_lng"
    }
}
