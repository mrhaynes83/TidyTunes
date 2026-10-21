package com.mrhaynes.routeplanner

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class MapsLinksTest {

    private fun stops(count: Int) = List(count) { GeoPoint(40.0 + it / 100.0, -74.0) }

    @Test
    fun splitsLongRoutesIntoPartsOfTenStops() {
        val parts = MapsLinks.parts(null, stops(25))
        assertEquals(3, parts.size)
        assertEquals(listOf(0 to 9, 10 to 19, 20 to 24), parts.map { it.fromIndex to it.toIndex })

        // First part starts from the phone's location, so no origin is given.
        assertFalse(parts[0].url.contains("origin="))
        // Later parts start where the previous part ended.
        assertTrue(parts[1].url.contains("origin=40.090000,-74.000000"))
        assertEquals(MapsLinks.MAX_WAYPOINTS, parts[0].url.substringAfter("waypoints=").substringBefore("&").split("%7C").size)
    }

    @Test
    fun singleStopHasNoWaypoints() {
        val url = MapsLinks.parts(GeoPoint(1.0, 2.0), stops(1)).single().url
        assertEquals(
            "https://www.google.com/maps/dir/?api=1&origin=1.000000,2.000000&destination=40.000000,-74.000000&travelmode=driving",
            url,
        )
    }
}
