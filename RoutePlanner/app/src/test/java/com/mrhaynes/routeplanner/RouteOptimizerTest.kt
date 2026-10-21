package com.mrhaynes.routeplanner

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.random.Random

class RouteOptimizerTest {

    private fun randomPoints(count: Int, random: Random) = List(count) {
        GeoPoint(40.0 + random.nextDouble() * 0.3, -74.0 + random.nextDouble() * 0.3)
    }

    private fun permutations(items: List<Int>): List<List<Int>> =
        if (items.size <= 1) listOf(items)
        else items.flatMap { first -> permutations(items - first).map { listOf(first) + it } }

    private fun bruteForce(start: GeoPoint, stops: List<GeoPoint>, end: GeoPoint?): Double =
        permutations(stops.indices.toList()).minOf { order ->
            RouteOptimizer.routeLength(start, order.map { stops[it] }, end)
        }

    @Test
    fun matchesBruteForceOnSmallRoutes() {
        val random = Random(7)
        repeat(15) {
            val start = randomPoints(1, random)[0]
            val elsewhere = randomPoints(1, random)[0]
            val stops = randomPoints(7, random)
            // Open route, round trip, and a route that must finish somewhere else.
            for (end in listOf(null, start, elsewhere)) {
                val order = RouteOptimizer.optimize(start, stops, end)
                val got = RouteOptimizer.routeLength(start, order.map { stops[it] }, end)
                val best = bruteForce(start, stops, end)
                assertEquals("end=$end", best, got, 1e-6)
            }
        }
    }

    @Test
    fun handlesManyStopsAndReturnsEveryStopOnce() {
        val random = Random(11)
        for (count in listOf(20, 25, 50, 120)) {
            val start = randomPoints(1, random)[0]
            val stops = randomPoints(count, random)
            val order = RouteOptimizer.optimize(start, stops, end = start)
            assertEquals(stops.indices.toList(), order.sorted())

            val optimized = RouteOptimizer.routeLength(start, order.map { stops[it] }, start)
            val asEntered = RouteOptimizer.routeLength(start, stops, start)
            assertTrue("optimized=$optimized entered=$asEntered", optimized < asEntered)
        }
    }

    @Test
    fun ordersPointsOnALine() {
        val start = GeoPoint(40.0, -74.0)
        val steps = listOf(5, 2, 9, 1, 7, 3, 8, 4, 6)
        val stops = steps.map { GeoPoint(40.0 + it * 0.01, -74.0) }
        val order = RouteOptimizer.optimize(start, stops, end = null)
        assertEquals(listOf(1, 2, 3, 4, 5, 6, 7, 8, 9), order.map { steps[it] })
    }

    @Test
    fun farthestFirstGoesOutThenWorksBackHome() {
        val home = GeoPoint(40.0, -74.0)
        val steps = listOf(5, 2, 9, 1, 7, 3, 8, 4, 6)
        val stops = steps.map { GeoPoint(40.0 + it * 0.01, -74.0) }
        for (returnHome in listOf(true, false)) {
            val order = RouteOptimizer.plan(home, stops, returnToBase = returnHome, farthestFirst = true)
            assertEquals(listOf(9, 8, 7, 6, 5, 4, 3, 2, 1), order.map { steps[it] })
        }
    }

    @Test
    fun farthestFirstAlwaysStartsAtTheFarthestStop() {
        val random = Random(3)
        repeat(10) {
            val home = randomPoints(1, random)[0]
            val stops = randomPoints(25, random)
            val order = RouteOptimizer.plan(home, stops, returnToBase = true, farthestFirst = true)
            val farthest = stops.indices.maxBy { RouteOptimizer.distanceMeters(home, stops[it]) }
            assertEquals(farthest, order.first())
            assertEquals(stops.indices.toList(), order.sorted())
        }
    }

    @Test
    fun trivialInputs() {
        val start = GeoPoint(0.0, 0.0)
        assertEquals(emptyList<Int>(), RouteOptimizer.optimize(start, emptyList(), start))
        assertEquals(listOf(0), RouteOptimizer.optimize(start, listOf(GeoPoint(1.0, 1.0)), start))
        assertEquals(listOf(0), RouteOptimizer.plan(start, listOf(GeoPoint(1.0, 1.0)), true, true))
    }
}
