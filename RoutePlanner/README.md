# TidyRoutes (Android)

Plan a multi-stop driving route and have the app put the stops in the most
efficient order, then drive it with Google Maps.

## Features

- Add stops by typing an address, pasting a whole list (one address per line),
  or long-pressing the map. Supports up to 300 stops.
- **Optimize route** reorders stops for the shortest trip, starting from your
  current location (or the first stop) and optionally returning to the start.
- Numbered markers and the route line on a Google Map.
- **Navigate** opens Google Maps turn-by-turn. Google Maps allows 10 stops per
  trip, so longer routes are split into parts (stops 1–10, 11–20, …).
- Check off stops as you finish them; re-optimize the rest any time.
- Drag stops to reorder by hand, swipe or tap the bin to delete.
- Your stops are saved between sessions.

## How optimization works

The app solves the route on the phone (nearest-neighbour, then 2-opt and Or-opt
improvements with random restarts) using straight-line distances. It needs no
paid API and matches the exact best order on small routes in tests. Road
layout (rivers, one-way streets) is not considered, so the order is very good
but not always perfect for driving time.

## Getting the APK

Every push that touches `RoutePlanner/` runs the **TidyRoutes APK** GitHub
Actions workflow, which runs the tests, builds `TidyRoutes.apk`, and attaches
it to a new GitHub release. Open the latest release on your phone, download the
APK, and allow installing from your browser when Android asks.

## Showing the map (Google Maps API key)

Without a key the app still plans routes and navigates in the Google Maps app,
but the in-app map is hidden. To turn it on:

1. In the Google Cloud Console, create a project, enable **Maps SDK for
   Android**, and create an API key (restrict it to Android apps with package
   `com.mrhaynes.routeplanner` if you like).
2. In this GitHub repo: Settings → Secrets and variables → Actions → New
   repository secret, name `MAPS_API_KEY`, value = the key.
3. Re-run the workflow (Actions → TidyRoutes APK → Run workflow) and install
   the new APK.

For local builds, put `MAPS_API_KEY=your-key` in `RoutePlanner/local.properties`
and run `./gradlew assembleDebug`.
