package com.mrhaynes.routeplanner

import kotlin.math.asin
import kotlin.math.cos
import kotlin.math.min
import kotlin.math.pow
import kotlin.math.sin
import kotlin.math.sqrt
import kotlin.random.Random

data class GeoPoint(val lat: Double, val lng: Double)

/**
 * Orders stops into a short route (a travelling-salesman heuristic).
 *
 * Uses straight-line distances, a nearest-neighbour start, then 2-opt and
 * Or-opt improvements with several random restarts. Handles hundreds of stops
 * in well under a second on a phone.
 */
object RouteOptimizer {
    private const val EARTH_RADIUS_M = 6_371_000.0

    fun distanceMeters(a: GeoPoint, b: GeoPoint): Double {
        val dLat = Math.toRadians(b.lat - a.lat)
        val dLng = Math.toRadians(b.lng - a.lng)
        val h = sin(dLat / 2).pow(2) +
            cos(Math.toRadians(a.lat)) * cos(Math.toRadians(b.lat)) * sin(dLng / 2).pow(2)
        return 2 * EARTH_RADIUS_M * asin(min(1.0, sqrt(h)))
    }

    /** Total length of visiting [stops] in the given order, starting at [start]. */
    fun routeLength(start: GeoPoint, stops: List<GeoPoint>, roundTrip: Boolean): Double {
        if (stops.isEmpty()) return 0.0
        var total = distanceMeters(start, stops[0])
        for (i in 1 until stops.size) total += distanceMeters(stops[i - 1], stops[i])
        if (roundTrip) total += distanceMeters(stops.last(), start)
        return total
    }

    /**
     * Returns indices into [stops] in the order they should be visited.
     * The route begins at [start]; if [roundTrip] it also ends there.
     */
    fun optimize(
        start: GeoPoint,
        stops: List<GeoPoint>,
        roundTrip: Boolean,
        restarts: Int = defaultRestarts(stops.size),
        seed: Long = 42L,
    ): List<Int> {
        val n = stops.size
        if (n <= 1) return List(n) { it }

        val points = listOf(start) + stops
        val dist = Array(points.size) { i ->
            DoubleArray(points.size) { j -> distanceMeters(points[i], points[j]) }
        }
        val solver = Solver(dist, roundTrip)

        var best = solver.improve(solver.nearestNeighbor())
        var bestCost = solver.cost(best)
        val random = Random(seed)
        repeat(restarts) {
            val tour = solver.improve(solver.randomTour(random))
            val cost = solver.cost(tour)
            if (cost < bestCost - 1e-9) {
                best = tour
                bestCost = cost
            }
        }
        // Drop the start node (index 0) and shift back to stop indices.
        return best.drop(1).map { it - 1 }
    }

    private fun defaultRestarts(n: Int) = when {
        n <= 15 -> 40
        n <= 60 -> 25
        n <= 150 -> 8
        else -> 2
    }

    /** Works on paths of node indices where node 0 (the start) is always first. */
    private class Solver(private val d: Array<DoubleArray>, private val closed: Boolean) {
        private val size = d.size
        private val eps = 1e-9

        fun cost(path: List<Int>): Double {
            var total = 0.0
            for (i in 1 until path.size) total += d[path[i - 1]][path[i]]
            if (closed) total += d[path.last()][path[0]]
            return total
        }

        fun nearestNeighbor(): MutableList<Int> {
            val visited = BooleanArray(size)
            val path = mutableListOf(0)
            visited[0] = true
            repeat(size - 1) {
                val last = path.last()
                var bestNode = -1
                var bestDist = Double.MAX_VALUE
                for (j in 0 until size) {
                    if (!visited[j] && d[last][j] < bestDist) {
                        bestDist = d[last][j]
                        bestNode = j
                    }
                }
                visited[bestNode] = true
                path.add(bestNode)
            }
            return path
        }

        fun randomTour(random: Random): MutableList<Int> =
            (mutableListOf(0) + (1 until size).shuffled(random)).toMutableList()

        fun improve(path: MutableList<Int>): MutableList<Int> {
            var improved = true
            while (improved) {
                improved = twoOpt(path) or orOpt(path)
            }
            return path
        }

        /** Node after position [k], or -1 if the path is open and [k] is last. */
        private fun next(path: List<Int>, k: Int): Int = when {
            k < path.size - 1 -> path[k + 1]
            closed -> path[0]
            else -> -1
        }

        private fun dist(a: Int, b: Int): Double = if (a < 0 || b < 0) 0.0 else d[a][b]

        /** Reverses a section of the path when that makes it shorter. */
        private fun twoOpt(path: MutableList<Int>): Boolean {
            var any = false
            val n = path.size
            var changed = true
            while (changed) {
                changed = false
                for (i in 1 until n - 1) {
                    val a = path[i - 1]
                    val b = path[i]
                    for (j in i + 1 until n) {
                        val c = path[j]
                        val e = next(path, j)
                        val delta = dist(a, c) + dist(b, e) - dist(a, b) - dist(c, e)
                        if (delta < -eps) {
                            path.subList(i, j + 1).reverse()
                            changed = true
                            any = true
                            break
                        }
                    }
                    if (changed) break
                }
            }
            return any
        }

        /** Moves runs of 1-3 stops (optionally flipped) to a better spot. */
        private fun orOpt(path: MutableList<Int>): Boolean {
            val n = path.size
            for (len in 1..3) {
                for (i in 1..n - len) {
                    val prev = path[i - 1]
                    val first = path[i]
                    val last = path[i + len - 1]
                    val after = next(path, i + len - 1)
                    val removeGain = dist(prev, first) + dist(last, after) - dist(prev, after)

                    for (p in 0 until n) {
                        if (p >= i - 1 && p <= i + len - 1) continue
                        val x = path[p]
                        val y = next(path, p)
                        val forward = dist(x, first) + dist(last, y) - dist(x, y)
                        val reversed = dist(x, last) + dist(first, y) - dist(x, y)
                        val insertCost = min(forward, reversed)
                        if (insertCost - removeGain < -eps) {
                            val segment = path.subList(i, i + len).toMutableList()
                            if (reversed < forward) segment.reverse()
                            repeat(len) { path.removeAt(i) }
                            val at = path.indexOf(x) + 1
                            path.addAll(at, segment)
                            return true
                        }
                    }
                }
            }
            return false
        }
    }
}
