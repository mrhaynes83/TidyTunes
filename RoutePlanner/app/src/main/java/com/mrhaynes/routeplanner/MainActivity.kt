package com.mrhaynes.routeplanner

import android.Manifest
import android.annotation.SuppressLint
import android.content.ActivityNotFoundException
import android.content.ClipboardManager
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Typeface
import android.net.Uri
import android.os.Bundle
import android.text.InputType
import android.view.Menu
import android.view.MenuItem
import android.view.View
import android.view.inputmethod.EditorInfo
import android.view.inputmethod.InputMethodManager
import android.widget.Button
import android.widget.CompoundButton
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
    private lateinit var homeChip: CompoundButton
    private lateinit var startFromLocation: CompoundButton
    private lateinit var roundTrip: CompoundButton
    private lateinit var farthestFirst: CompoundButton

    private val stops = mutableListOf<Stop>()
    private var googleMap: GoogleMap? = null
    private var lastLocation: GeoPoint? = null
    private var home: Home? = null
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
        homeChip = findViewById(R.id.chip_home)
        startFromLocation = findViewById(R.id.chip_start_location)
        roundTrip = findViewById(R.id.chip_round_trip)
        farthestFirst = findViewById(R.id.chip_farthest)

        home = store.home
        homeChip.isChecked = store.useHome && home != null
        startFromLocation.isChecked = store.startFromLocation
        roundTrip.isChecked = store.roundTrip
        farthestFirst.isChecked = store.farthestFirst
        updateChipStates()

        homeChip.setOnCheckedChangeListener { _, checked ->
            if (checked && home == null) {
                // No home yet: ask for it, and turn this on once it's saved.
                homeChip.isChecked = false
                showHomeDialog()
                return@setOnCheckedChangeListener
            }
            store.useHome = checked
            updateChipStates()
            refresh(fitMap = true)
        }
        homeChip.setOnLongClickListener {
            showHomeDialog()
            true
        }
        startFromLocation.setOnCheckedChangeListener { _, checked ->
            store.startFromLocation = checked
            if (checked) ensureLocationPermission()
            refresh()
        }
        roundTrip.setOnCheckedChangeListener { _, checked ->
            store.roundTrip = checked
            refresh()
        }
        farthestFirst.setOnCheckedChangeListener { _, checked ->
            store.farthestFirst = checked
        }

        setupList()
        setupMap(savedInstanceState)

        findViewById<Button>(R.id.btn_add).setOnClickListener { addFromInput() }
        findViewById<Button>(R.id.btn_paste).setOnClickListener { showPasteDialog() }
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
        hideKeyboard()
        // Several addresses pasted into the box: let the user review them.
        val several = AddressExtractor.extract(text)
        if (several.size >= 2) {
            input.setText("")
            reviewAndAdd(several)
            return
        }
        if (!geocoding.isAvailable && Geocoding.parseLatLng(text) == null) {
            toast(getString(R.string.geocoder_missing))
            return
        }
        runBusy {
            val result = geocoding.search(text, biasFor(text))
            if (result == null) {
                toast(getString(R.string.address_not_found, text))
            } else {
                addStop(result.label, result.point)
                input.setText("")
            }
        }
    }

    /** Addresses without a city are looked up near the route's area first. */
    private fun biasFor(address: String, placed: List<GeoPoint> = emptyList()): GeoPoint? =
        if (AddressExtractor.hasLocality(address)) null else areaHint(placed)

    /** Middle of the stops just added, else of the existing stops, else the phone or home. */
    private fun areaHint(placed: List<GeoPoint>): GeoPoint? {
        val points = placed.ifEmpty { stops.map { it.point } }
        if (points.isNotEmpty()) {
            return GeoPoint(points.sumOf { it.lat } / points.size, points.sumOf { it.lng } / points.size)
        }
        return lastLocation ?: home?.point
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

    /** Paste any text (email, spreadsheet, notes); the addresses in it are picked out. */
    private fun showPasteDialog() {
        val view = layoutInflater.inflate(R.layout.dialog_bulk_add, null)
        val field = view.findViewById<EditText>(R.id.bulk_input)
        clipboardText()?.let {
            field.setText(it)
            field.selectAll()
        }
        AlertDialog.Builder(this)
            .setTitle(R.string.paste_title)
            .setMessage(R.string.bulk_hint)
            .setView(view)
            .setPositiveButton(R.string.find_addresses) { _, _ -> findAddresses(field.text.toString()) }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun clipboardText(): String? {
        val clipboard = getSystemService(ClipboardManager::class.java) ?: return null
        val clip = clipboard.primaryClip ?: return null
        if (clip.itemCount == 0) return null
        return clip.getItemAt(0).coerceToText(this)?.toString()?.takeIf { it.isNotBlank() }
    }

    private fun findAddresses(text: String) {
        if (text.isBlank()) return
        runBusy {
            val found = AddressFinder.find(this@MainActivity, text)
            if (found.isNotEmpty()) {
                reviewAndAdd(found)
                return@runBusy
            }
            val lines = text.lines().map { it.trim() }.filter { it.isNotEmpty() }
            AlertDialog.Builder(this@MainActivity)
                .setTitle(R.string.none_found_title)
                .setMessage(R.string.none_found_message)
                .setPositiveButton(R.string.use_lines) { _, _ -> reviewAndAdd(lines) }
                .setNegativeButton(android.R.string.cancel, null)
                .show()
        }
    }

    /** Shows the addresses found, all ticked, so the user can drop any before adding. */
    private fun reviewAndAdd(candidates: List<String>) {
        if (candidates.isEmpty()) return
        val room = MAX_STOPS - stops.size
        val items = candidates.take(maxOf(room, 0))
        if (items.isEmpty()) {
            toast(getString(R.string.too_many_stops, MAX_STOPS))
            return
        }
        val checked = BooleanArray(items.size) { true }
        AlertDialog.Builder(this)
            .setTitle(getString(R.string.found_title, items.size))
            .setMultiChoiceItems(items.toTypedArray(), checked) { _, which, isChecked -> checked[which] = isChecked }
            .setPositiveButton(R.string.add_selected) { _, _ ->
                addMany(items.filterIndexed { i, _ -> checked[i] })
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun addMany(addresses: List<String>) {
        if (addresses.isEmpty()) return
        runBusy {
            val failed = mutableListOf<String>()
            val placed = mutableListOf<GeoPoint>()
            // Addresses with a town go first, so ones without can be found near them.
            val (withTown, withoutTown) = addresses.partition { AddressExtractor.hasLocality(it) }
            (withTown + withoutTown).forEachIndexed { i, address ->
                summary.text = getString(R.string.finding_progress, i + 1, addresses.size)
                val result = geocoding.search(address, biasFor(address, placed))
                if (result == null) {
                    failed.add(address)
                } else {
                    addStop(result.label, result.point)
                    placed.add(result.point)
                }
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
            val homePoint = home?.point?.takeIf { usingHome }
            val wantsLocation = homePoint == null && startFromLocation.isChecked
            val location = if (wantsLocation) currentLocation() else null
            if (wantsLocation && location == null) {
                toast(getString(R.string.no_location_fallback))
            }
            val base = homePoint ?: location
            // With no home or location, the first pending stop stays first and is the start.
            val fixedFirst: Stop? = if (base == null) pending.first() else null
            val toOrder = if (fixedFirst != null) pending.drop(1) else pending
            val origin: GeoPoint = base ?: pending.first().point
            val end = if (returnsToStart) origin else null

            val points = toOrder.map { it.point }
            val before = RouteOptimizer.routeLength(origin, points, end)
            val order = withContext(Dispatchers.Default) {
                RouteOptimizer.plan(origin, points, returnsToStart, farthestFirst.isChecked)
            }
            val ordered = order.map { toOrder[it] }
            val after = RouteOptimizer.routeLength(origin, ordered.map { it.point }, end)

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
        routeStart()?.takeIf { returnsToStart }?.let { destinations.add(it) }

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
        else getString(if (usingHome) R.string.home_label else R.string.start_label)

    private val usingHome: Boolean get() = homeChip.isChecked && home != null

    /** Home always means a round trip; otherwise it's the "Return to start" setting. */
    private val returnsToStart: Boolean get() = usingHome || roundTrip.isChecked

    /** Where the route begins: home, the phone's location, or the first stop. */
    private fun routeStart(): GeoPoint? = when {
        usingHome -> home?.point
        startFromLocation.isChecked -> lastLocation ?: stops.firstOrNull()?.point
        else -> stops.firstOrNull()?.point
    }

    /** Start point drawn on the map and used for distances, or null to start at the first stop. */
    private fun startPoint(): GeoPoint? = when {
        usingHome -> home?.point
        startFromLocation.isChecked -> lastLocation
        else -> null
    }

    // ---------- Home ----------

    private fun updateChipStates() {
        // Home sets both the start and the end, so these two don't apply.
        startFromLocation.isEnabled = !usingHome
        roundTrip.isEnabled = !usingHome
    }

    private fun showHomeDialog() {
        val density = resources.displayMetrics.density
        val field = EditText(this).apply {
            hint = getString(R.string.home_hint)
            inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_POSTAL_ADDRESS
            isSingleLine = true
            setText(home?.label.orEmpty())
        }
        val container = FrameLayout(this).apply {
            val side = (20 * density).toInt()
            setPadding(side, (8 * density).toInt(), side, 0)
            addView(field)
        }
        AlertDialog.Builder(this)
            .setTitle(R.string.home_title)
            .setMessage(R.string.home_message)
            .setView(container)
            .setPositiveButton(R.string.save) { _, _ -> saveHomeFromText(field.text.toString()) }
            .setNeutralButton(R.string.use_my_location) { _, _ -> saveHomeFromLocation() }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun saveHomeFromText(text: String) {
        val query = text.trim()
        if (query.isEmpty()) return
        runBusy {
            val result = geocoding.search(query, biasFor(query))
            if (result == null) {
                toast(getString(R.string.address_not_found, query))
            } else {
                setHome(Home(result.label, result.point.lat, result.point.lng))
            }
        }
    }

    private fun saveHomeFromLocation() {
        if (!hasLocationPermission()) {
            ensureLocationPermission()
            toast(getString(R.string.no_location))
            return
        }
        runBusy {
            val location = currentLocation()
            if (location == null) {
                toast(getString(R.string.no_location))
            } else {
                setHome(Home(geocoding.describe(location), location.lat, location.lng))
            }
        }
    }

    private fun setHome(newHome: Home) {
        home = newHome
        store.home = newHome
        if (homeChip.isChecked) {
            refresh(fitMap = true)
        } else {
            homeChip.isChecked = true // listener saves the setting and redraws
        }
        updateChipStates()
        toast(getString(R.string.home_saved, newHome.label))
    }

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
            R.id.action_bulk_add -> showPasteDialog()
            R.id.action_set_home -> showHomeDialog()
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
        val start = startPoint()
        val points = pending.map { it.point }
        val length = when {
            points.isEmpty() -> 0.0
            start != null ->
                RouteOptimizer.routeLength(start, points, start.takeIf { returnsToStart })
            else ->
                RouteOptimizer.routeLength(points.first(), points.drop(1), points.first().takeIf { returnsToStart })
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

        home?.let {
            map.addMarker(
                MarkerOptions()
                    .position(LatLng(it.lat, it.lng))
                    .title(getString(R.string.home_label) + ": " + it.label)
                    .icon(BitmapDescriptorFactory.defaultMarker(BitmapDescriptorFactory.HUE_AZURE)),
            )
        }
        val start = startPoint()
        if (start != null) path.add(LatLng(start.lat, start.lng))

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
        if (returnsToStart && path.size > 1) path.add(path.first())

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
            listOfNotNull(start?.let { LatLng(it.lat, it.lng) })
        try {
            when {
                all.isEmpty() -> (lastLocation ?: home?.point)?.let {
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
