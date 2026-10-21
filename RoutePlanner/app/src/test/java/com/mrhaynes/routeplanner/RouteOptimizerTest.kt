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

    private fun bruteForce(start: GeoPoint, stops: List<GeoPoint>, roundTrip: Boolean): Double =
        permutations(stops.indices.toList()).minOf { order ->
            RouteOptimizer.routeLength(start, order.map { stops[it] }, roundTrip)
        }

    @Test
    fun matchesBruteForceOnSmallRoutes() {
        val random = Random(7)
        repeat(15) {
            val start = randomPoints(1, random)[0]
            val stops = randomPoints(7, random)
            for (roundTrip in listOf(true, false)) {
                val order = RouteOptimizer.optimize(start, stops, roundTrip)
                val got = RouteOptimizer.routeLength(start, order.map { stops[it] }, roundTrip)
                val best = bruteForce(start, stops, roundTrip)
                assertEquals("roundTrip=$roundTrip", best, got, 1e-6)
            }
        }
    }

    @Test
    fun handlesManyStopsAndReturnsEveryStopOnce() {
        val random = Random(11)
        for (count in listOf(20, 25, 50, 120)) {
            val start = randomPoints(1, random)[0]
            val stops = randomPoints(count, random)
            val order = RouteOptimizer.optimize(start, stops, roundTrip = true)
            assertEquals(stops.indices.toList(), order.sorted())

            val optimized = RouteOptimizer.routeLength(start, order.map { stops[it] }, true)
            val asEntered = RouteOptimizer.routeLength(start, stops, true)
            assertTrue("optimized=$optimized entered=$asEntered", optimized < asEntered)
        }
    }

    @Test
    fun ordersPointsOnALine() {
        val start = GeoPoint(40.0, -74.0)
        val steps = listOf(5, 2, 9, 1, 7, 3, 8, 4, 6)
        val stops = steps.map { GeoPoint(40.0 + it * 0.01, -74.0) }
        val order = RouteOptimizer.optimize(start, stops, roundTrip = false)
        assertEquals(listOf(1, 2, 3, 4, 5, 6, 7, 8, 9), order.map { steps[it] })
    }

    @Test
    fun trivialInputs() {
        val start = GeoPoint(0.0, 0.0)
        assertEquals(emptyList<Int>(), RouteOptimizer.optimize(start, emptyList(), true))
        assertEquals(listOf(0), RouteOptimizer.optimize(start, listOf(GeoPoint(1.0, 1.0)), true))
    }
}
