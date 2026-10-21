package com.mrhaynes.routeplanner

import android.Manifest
import android.annotation.SuppressLint
import android.content.ActivityNotFoundException
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Typeface
import android.net.Uri
import android.os.Bundle
import android.view.Menu
import android.view.MenuItem
import android.view.View
import android.view.inputmethod.EditorInfo
import android.view.inputmethod.InputMethodManager
import android.widget.Button
import android.widget.CheckBox
import android.widget.EditText
import android.widget.FrameLayout
import android.widget.ProgressBar
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.ItemTouchHelper
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.maps.CameraUpdateFactory
import com.google.android.gms.maps.GoogleMap
import com.google.android.gms.maps.SupportMapFragment
import com.google.android.gms.maps.model.BitmapDescriptor
import com.google.android.gms.maps.model.BitmapDescriptorFactory
import com.google.android.gms.maps.model.LatLng
import com.google.android.gms.maps.model.LatLngBounds
import com.google.android.gms.maps.model.MarkerOptions
import com.google.android.gms.maps.model.PolylineOptions
import com.google.android.gms.tasks.CancellationTokenSource
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeoutOrNull
import java.util.Collections
import java.util.Locale
import kotlin.coroutines.resume

class MainActivity : AppCompatActivity(), StopAdapter.Listener {

    private lateinit var store: StopStore
    private lateinit var geocoding: Geocoding
    private lateinit var adapter: StopAdapter
    private lateinit var touchHelper: ItemTouchHelper

    private lateinit var input: EditText
    private lateinit var summary: TextView
    private lateinit var progress: ProgressBar
    private lateinit var startFromLocation: CheckBox
    private lateinit var roundTrip: CheckBox

    private val stops = mutableListOf<Stop>()
    private var googleMap: GoogleMap? = null
    private var lastLocation: GeoPoint? = null
    private var busy = false

    private val useMiles = Locale.getDefault().country in setOf("US", "GB", "LR", "MM")

    private val permissionRequest = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions(),
    ) { onLocationPermissionResult() }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        store = StopStore(this)
        geocoding = Geocoding(this)
        stops.addAll(store.load())

        input = findViewById(R.id.input_address)
        summary = findViewById(R.id.summary)
        progress = findViewById(R.id.progress)
        startFromLocation = findViewById(R.id.cb_start_location)
        roundTrip = findViewById(R.id.cb_round_trip)

        startFromLocation.isChecked = store.startFromLocation
        roundTrip.isChecked = store.roundTrip
        startFromLocation.setOnCheckedChangeListener { _, checked ->
            store.startFromLocation = checked
            if (checked) ensureLocationPermission()
            refresh()
        }
        roundTrip.setOnCheckedChangeListener { _, checked ->
            store.roundTrip = checked
            refresh()
        }

        setupList()
        setupMap(savedInstanceState)

        findViewById<Button>(R.id.btn_add).setOnClickListener { addFromInput() }
        input.setOnEditorActionListener { _, actionId, _ ->
            if (actionId == EditorInfo.IME_ACTION_DONE || actionId == EditorInfo.IME_ACTION_GO) {
                addFromInput()
                true
            } else {
                false
            }
        }
        findViewById<Button>(R.id.btn_optimize).setOnClickListener { optimizeRoute() }
        findViewById<Button>(R.id.btn_navigate).setOnClickListener { navigate() }

        ensureLocationPermission()
        refresh(fitMap = true)
    }

    // ---------- Setup ----------

    private fun setupList() {
        val list = findViewById<RecyclerView>(R.id.stop_list)
        adapter = StopAdapter(stops, this)
        list.layoutManager = LinearLayoutManager(this)
        list.adapter = adapter

        touchHelper = ItemTouchHelper(object : ItemTouchHelper.SimpleCallback(
            ItemTouchHelper.UP or ItemTouchHelper.DOWN,
            ItemTouchHelper.LEFT or ItemTouchHelper.RIGHT,
        ) {
            override fun onMove(
                rv: RecyclerView,
                from: RecyclerView.ViewHolder,
                to: RecyclerView.ViewHolder,
            ): Boolean {
                val a = from.bindingAdapterPosition
                val b = to.bindingAdapterPosition
                if (a == RecyclerView.NO_POSITION || b == RecyclerView.NO_POSITION) return false
                Collections.swap(stops, a, b)
                adapter.notifyItemMoved(a, b)
                return true
            }

            override fun onSwiped(holder: RecyclerView.ViewHolder, direction: Int) {
                val pos = holder.bindingAdapterPosition
                if (pos != RecyclerView.NO_POSITION) onDeleteClicked(pos)
            }

            override fun clearView(rv: RecyclerView, holder: RecyclerView.ViewHolder) {
                super.clearView(rv, holder)
                // Drag finished: renumber and redraw.
                refresh()
            }
        })
        touchHelper.attachToRecyclerView(list)
    }

    private fun setupMap(savedInstanceState: Bundle?) {
        val container = findViewById<FrameLayout>(R.id.map_container)
        val notice = findViewById<TextView>(R.id.map_disabled)
        if (!BuildConfig.HAS_MAPS_KEY) {
            // Without an API key the map can't load, but planning and
            // navigation through the Google Maps app still work.
            notice.visibility = View.VISIBLE
            return
        }
        notice.visibility = View.GONE
        val fragment = if (savedInstanceState == null) {
            SupportMapFragment.newInstance().also {
                supportFragmentManager.beginTransaction().replace(container.id, it).commitNow()
            }
        } else {
            supportFragmentManager.findFragmentById(container.id) as SupportMapFragment
        }
        fragment.getMapAsync { map ->
            googleMap = map
            map.uiSettings.isZoomControlsEnabled = true
            map.setOnMapLongClickListener { latLng -> addFromMap(GeoPoint(latLng.latitude, latLng.longitude)) }
            map.setOnMapLoadedCallback { renderMap(fit = true) }
            enableMyLocationLayer()
            renderMap(fit = true)
        }
    }

    // ---------- Adding stops ----------

    private fun addFromInput() {
        val text = input.text.toString().trim()
        if (text.isEmpty()) return
        if (!geocoding.isAvailable && Geocoding.parseLatLng(text) == null) {
            toast(getString(R.string.geocoder_missing))
            return
        }
        hideKeyboard()
        runBusy {
            val result = geocoding.search(text)
            if (result == null) {
                toast(getString(R.string.address_not_found, text))
            } else {
                addStop(result.label, result.point)
                input.setText("")
            }
        }
    }

    private fun addFromMap(point: GeoPoint) {
        runBusy {
            addStop(geocoding.describe(point), point)
        }
    }

    private fun addStop(label: String, point: GeoPoint) {
        if (stops.size >= MAX_STOPS) {
            toast(getString(R.string.too_many_stops, MAX_STOPS))
            return
        }
        val id = (stops.maxOfOrNull { it.id } ?: 0L) + 1
        stops.add(Stop(id, label, point.lat, point.lng))
        adapter.notifyItemInserted(stops.size - 1)
        refresh(fitMap = true)
    }

    private fun showBulkAdd() {
        val view = layoutInflater.inflate(R.layout.dialog_bulk_add, null)
        val field = view.findViewById<EditText>(R.id.bulk_input)
        AlertDialog.Builder(this)
            .setTitle(R.string.bulk_title)
            .setView(view)
            .setPositiveButton(R.string.bulk_add) { _, _ ->
                val lines = field.text.lines().map { it.trim() }.filter { it.isNotEmpty() }
                addMany(lines)
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun addMany(lines: List<String>) {
        if (lines.isEmpty()) return
        runBusy {
            val failed = mutableListOf<String>()
            lines.forEachIndexed { i, line ->
                summary.text = getString(R.string.finding_progress, i + 1, lines.size)
                val result = geocoding.search(line)
                if (result == null) failed.add(line) else addStop(result.label, result.point)
            }
            refresh(fitMap = true)
            if (failed.isNotEmpty()) {
                AlertDialog.Builder(this@MainActivity)
                    .setTitle(getString(R.string.bulk_failed_title, failed.size))
                    .setMessage(failed.joinToString("\n"))
                    .setPositiveButton(android.R.string.ok, null)
                    .show()
            }
        }
    }

    // ---------- Optimizing ----------

    private fun optimizeRoute() {
        val completed = stops.filter { it.done }
        val pending = stops.filter { !it.done }
        if (pending.size < 2) {
            toast(getString(R.string.need_two_stops))
            return
        }
        runBusy {
            val location = if (startFromLocation.isChecked) currentLocation() else null
            if (startFromLocation.isChecked && location == null) {
                toast(getString(R.string.no_location_fallback))
            }
            // With no location, the first pending stop stays first and is the start.
            val fixedFirst: Stop? = if (location == null) pending.first() else null
            val toOrder = if (fixedFirst != null) pending.drop(1) else pending
            val origin: GeoPoint = location ?: pending.first().point

            val points = toOrder.map { it.point }
            val before = RouteOptimizer.routeLength(origin, points, roundTrip.isChecked)
            val order = withContext(Dispatchers.Default) {
                RouteOptimizer.optimize(origin, points, roundTrip.isChecked)
            }
            val ordered = order.map { toOrder[it] }
            val after = RouteOptimizer.routeLength(origin, ordered.map { it.point }, roundTrip.isChecked)

            stops.clear()
            stops.addAll(completed + listOfNotNull(fixedFirst) + ordered)
            adapter.notifyDataSetChanged()
            refresh(fitMap = true)
            toast(getString(R.string.optimized_result, formatDistance(after), formatDistance(before)))
        }
    }

    // ---------- Navigation ----------

    private fun navigate() {
        val pendingIndexes = stops.indices.filter { !stops[it].done }
        if (pendingIndexes.isEmpty()) {
            toast(getString(R.string.nothing_to_navigate))
            return
        }
        val destinations = pendingIndexes.map { stops[it].point }.toMutableList()
        routeStart()?.takeIf { roundTrip.isChecked }?.let { destinations.add(it) }

        // Origin is left empty so Google Maps starts from where the phone is now.
        val parts = MapsLinks.parts(null, destinations)
        if (parts.size == 1) {
            openMaps(parts[0].url)
            return
        }
        val labels = parts.mapIndexed { i, part ->
            val first = stopNumberLabel(pendingIndexes, part.fromIndex)
            val last = stopNumberLabel(pendingIndexes, part.toIndex)
            getString(R.string.nav_part, i + 1, first, last)
        }.toTypedArray()
        AlertDialog.Builder(this)
            .setTitle(R.string.nav_parts_title)
            .setItems(labels) { _, which -> openMaps(parts[which].url) }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun stopNumberLabel(pendingIndexes: List<Int>, destIndex: Int): String =
        if (destIndex < pendingIndexes.size) (pendingIndexes[destIndex] + 1).toString()
        else getString(R.string.start_label)

    /** Where the route begins: the phone's location or the first stop. */
    private fun routeStart(): GeoPoint? =
        if (startFromLocation.isChecked) lastLocation ?: stops.firstOrNull()?.point
        else stops.firstOrNull()?.point

    private fun openMaps(url: String) {
        val intent = Intent(Intent.ACTION_VIEW, Uri.parse(url)).setPackage(MAPS_PACKAGE)
        try {
            startActivity(intent)
        } catch (e: ActivityNotFoundException) {
            // Google Maps app not installed: open in the browser instead.
            startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
        }
    }

    // ---------- List callbacks ----------

    override fun onStopClicked(position: Int) {
        val stop = stops[position]
        val toggle = if (stop.done) R.string.mark_not_done else R.string.mark_done
        val options = arrayOf(
            getString(R.string.navigate_here),
            getString(R.string.show_on_map),
            getString(toggle),
            getString(R.string.delete),
        )
        AlertDialog.Builder(this)
            .setTitle("${position + 1}. ${stop.label}")
            .setItems(options) { _, which ->
                when (which) {
                    0 -> navigateTo(stop)
                    1 -> googleMap?.animateCamera(CameraUpdateFactory.newLatLngZoom(LatLng(stop.lat, stop.lng), 16f))
                    2 -> onDoneChanged(position, !stop.done)
                    3 -> onDeleteClicked(position)
                }
            }
            .show()
    }

    private fun navigateTo(stop: Stop) {
        val intent = Intent(Intent.ACTION_VIEW, Uri.parse(MapsLinks.navigateToUri(stop.point)))
            .setPackage(MAPS_PACKAGE)
        try {
            startActivity(intent)
        } catch (e: ActivityNotFoundException) {
            openMaps(MapsLinks.directionsUrl(null, emptyList(), stop.point))
        }
    }

    override fun onDoneChanged(position: Int, done: Boolean) {
        if (position !in stops.indices || stops[position].done == done) return
        stops[position] = stops[position].copy(done = done)
        adapter.notifyItemChanged(position)
        refresh()
    }

    override fun onDeleteClicked(position: Int) {
        if (position !in stops.indices) return
        stops.removeAt(position)
        adapter.notifyItemRemoved(position)
        adapter.notifyItemRangeChanged(position, stops.size - position)
        refresh()
    }

    override fun onStartDrag(holder: RecyclerView.ViewHolder) = touchHelper.startDrag(holder)

    // ---------- Menu ----------

    override fun onCreateOptionsMenu(menu: Menu): Boolean {
        menuInflater.inflate(R.menu.main_menu, menu)
        return true
    }

    @SuppressLint("NotifyDataSetChanged")
    override fun onOptionsItemSelected(item: MenuItem): Boolean {
        when (item.itemId) {
            R.id.action_bulk_add -> showBulkAdd()
            R.id.action_reverse -> {
                stops.reverse()
                adapter.notifyDataSetChanged()
                refresh(fitMap = true)
            }
            R.id.action_reset_done -> {
                stops.replaceAll { it.copy(done = false) }
                adapter.notifyDataSetChanged()
                refresh()
            }
            R.id.action_clear_done -> {
                stops.removeAll { it.done }
                adapter.notifyDataSetChanged()
                refresh(fitMap = true)
            }
            R.id.action_clear_all -> AlertDialog.Builder(this)
                .setMessage(R.string.confirm_clear)
                .setPositiveButton(R.string.clear) { _, _ ->
                    stops.clear()
                    adapter.notifyDataSetChanged()
                    refresh()
                }
                .setNegativeButton(android.R.string.cancel, null)
                .show()
            else -> return super.onOptionsItemSelected(item)
        }
        return true
    }

    // ---------- Location ----------

    private fun hasLocationPermission() =
        ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED ||
            ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_COARSE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED

    private fun ensureLocationPermission() {
        if (hasLocationPermission()) {
            onLocationPermissionResult()
        } else {
            permissionRequest.launch(
                arrayOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION),
            )
        }
    }

    private fun onLocationPermissionResult() {
        if (!hasLocationPermission()) return
        enableMyLocationLayer()
        lifecycleScope.launch {
            currentLocation()
            refresh()
        }
    }

    @SuppressLint("MissingPermission")
    private fun enableMyLocationLayer() {
        if (hasLocationPermission()) googleMap?.isMyLocationEnabled = true
    }

    @SuppressLint("MissingPermission")
    private suspend fun currentLocation(): GeoPoint? {
        if (!hasLocationPermission()) return null
        val client = LocationServices.getFusedLocationProviderClient(this)
        val found = withTimeoutOrNull(15_000) {
            suspendCancellableCoroutine { cont ->
                val token = CancellationTokenSource()
                cont.invokeOnCancellation { token.cancel() }
                client.getCurrentLocation(Priority.PRIORITY_HIGH_ACCURACY, token.token)
                    .addOnSuccessListener { loc ->
                        if (loc != null) {
                            if (cont.isActive) cont.resume(GeoPoint(loc.latitude, loc.longitude))
                        } else {
                            client.lastLocation
                                .addOnSuccessListener { last ->
                                    if (cont.isActive) cont.resume(last?.let { GeoPoint(it.latitude, it.longitude) })
                                }
                                .addOnFailureListener { if (cont.isActive) cont.resume(null) }
                        }
                    }
                    .addOnFailureListener { if (cont.isActive) cont.resume(null) }
            }
        }
        if (found != null) lastLocation = found
        return found ?: lastLocation
    }

    // ---------- Rendering ----------

    private fun refresh(fitMap: Boolean = false) {
        store.save(stops)
        adapter.notifyItemRangeChanged(0, stops.size)
        updateSummary()
        renderMap(fitMap)
    }

    private fun updateSummary() {
        val pending = stops.filter { !it.done }
        if (stops.isEmpty()) {
            summary.text = getString(R.string.empty_hint)
            return
        }
        val start = if (startFromLocation.isChecked) lastLocation else null
        val points = pending.map { it.point }
        val length = when {
            points.isEmpty() -> 0.0
            start != null -> RouteOptimizer.routeLength(start, points, roundTrip.isChecked)
            else -> RouteOptimizer.routeLength(points.first(), points.drop(1), roundTrip.isChecked)
        }
        summary.text = getString(
            R.string.summary,
            stops.size,
            pending.size,
            formatDistance(length),
        )
    }

    private fun renderMap(fit: Boolean) {
        val map = googleMap ?: return
        map.clear()
        val path = mutableListOf<LatLng>()

        val start = lastLocation
        if (startFromLocation.isChecked && start != null) path.add(LatLng(start.lat, start.lng))

        stops.forEachIndexed { i, stop ->
            val pos = LatLng(stop.lat, stop.lng)
            map.addMarker(
                MarkerOptions()
                    .position(pos)
                    .title("${i + 1}. ${stop.label}")
                    .icon(numberedMarker(i + 1, stop.done))
                    .anchor(0.5f, 0.5f),
            )
            if (!stop.done) path.add(pos)
        }
        if (roundTrip.isChecked && path.size > 1) path.add(path.first())

        if (path.size > 1) {
            map.addPolyline(
                PolylineOptions()
                    .addAll(path)
                    .width(8f)
                    .color(ContextCompat.getColor(this, R.color.route_line))
                    .geodesic(true),
            )
        }

        if (!fit) return
        val all = stops.map { LatLng(it.lat, it.lng) } +
            listOfNotNull(start?.takeIf { startFromLocation.isChecked }?.let { LatLng(it.lat, it.lng) })
        try {
            when {
                all.isEmpty() -> start?.let {
                    map.moveCamera(CameraUpdateFactory.newLatLngZoom(LatLng(it.lat, it.lng), 13f))
                }
                all.size == 1 -> map.animateCamera(CameraUpdateFactory.newLatLngZoom(all[0], 14f))
                else -> {
                    val bounds = LatLngBounds.builder().apply { all.forEach { include(it) } }.build()
                    val padding = (48 * resources.displayMetrics.density).toInt()
                    map.animateCamera(CameraUpdateFactory.newLatLngBounds(bounds, padding))
                }
            }
        } catch (e: IllegalStateException) {
            // Map not laid out yet; the map-loaded callback will fit it.
        }
    }

    private fun numberedMarker(number: Int, done: Boolean): BitmapDescriptor {
        val density = resources.displayMetrics.density
        val size = ((if (number >= 100) 36 else 30) * density).toInt()
        val bitmap = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(bitmap)
        val center = size / 2f
        val fill = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = if (done) Color.GRAY else ContextCompat.getColor(this@MainActivity, R.color.marker)
        }
        val ring = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = Color.WHITE
            style = Paint.Style.STROKE
            strokeWidth = 2 * density
        }
        val text = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = Color.WHITE
            textAlign = Paint.Align.CENTER
            typeface = Typeface.DEFAULT_BOLD
            textSize = (if (number >= 100) 12 else 13) * density
        }
        canvas.drawCircle(center, center, center - 2 * density, fill)
        canvas.drawCircle(center, center, center - 2 * density, ring)
        canvas.drawText(number.toString(), center, center - (text.descent() + text.ascent()) / 2, text)
        return BitmapDescriptorFactory.fromBitmap(bitmap)
    }

    // ---------- Helpers ----------

    private fun formatDistance(meters: Double): String =
        if (useMiles) String.format(Locale.getDefault(), "%.1f mi", meters / 1609.344)
        else String.format(Locale.getDefault(), "%.1f km", meters / 1000)

    /** Runs a task with a spinner, ignoring taps while one is already running. */
    private fun runBusy(block: suspend () -> Unit) {
        if (busy) return
        busy = true
        progress.visibility = View.VISIBLE
        lifecycleScope.launch {
            try {
                block()
            } finally {
                busy = false
                progress.visibility = View.GONE
                updateSummary()
            }
        }
    }

    private fun hideKeyboard() {
        val imm = getSystemService(InputMethodManager::class.java)
        imm?.hideSoftInputFromWindow(input.windowToken, 0)
    }

    private fun toast(message: String) = Toast.makeText(this, message, Toast.LENGTH_LONG).show()

    private companion object {
        const val MAPS_PACKAGE = "com.google.android.apps.maps"
        const val MAX_STOPS = 300
    }
}
