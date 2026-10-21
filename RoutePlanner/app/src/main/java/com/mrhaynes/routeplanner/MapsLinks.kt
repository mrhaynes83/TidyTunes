package com.mrhaynes.routeplanner

import java.util.Locale

/**
 * Builds Google Maps direction links. The Maps app accepts at most 9
 * waypoints per link, so long routes are split into parts of up to 10 stops.
 */
object MapsLinks {
    const val MAX_WAYPOINTS = 9
    private const val STOPS_PER_PART = MAX_WAYPOINTS + 1

    /** One link covering stops [fromIndex]..[toIndex] (inclusive) of the destination list. */
    data class Part(val fromIndex: Int, val toIndex: Int, val url: String)

    /**
     * @param origin where the route starts, or null to start at the phone's location.
     * @param destinations stops in visiting order (include the start again for a round trip).
     */
    fun parts(origin: GeoPoint?, destinations: List<GeoPoint>): List<Part> {
        val chunks = destinations.chunked(STOPS_PER_PART)
        var previousEnd = origin
        return chunks.mapIndexed { index, chunk ->
            val url = directionsUrl(previousEnd, chunk.dropLast(1), chunk.last())
            previousEnd = chunk.last()
            val from = index * STOPS_PER_PART
            Part(from, from + chunk.size - 1, url)
        }
    }

    fun directionsUrl(origin: GeoPoint?, waypoints: List<GeoPoint>, destination: GeoPoint): String {
        val sb = StringBuilder("https://www.google.com/maps/dir/?api=1")
        if (origin != null) sb.append("&origin=").append(format(origin))
        sb.append("&destination=").append(format(destination))
        if (waypoints.isNotEmpty()) {
            sb.append("&waypoints=").append(waypoints.joinToString("%7C") { format(it) })
        }
        sb.append("&travelmode=driving")
        return sb.toString()
    }

    fun navigateToUri(point: GeoPoint): String = "google.navigation:q=${format(point)}"

    private fun format(p: GeoPoint) = String.format(Locale.US, "%.6f,%.6f", p.lat, p.lng)
}
