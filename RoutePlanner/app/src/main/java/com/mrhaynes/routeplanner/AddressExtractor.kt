package com.mrhaynes.routeplanner

/**
 * Pulls street addresses (US and Canada style) out of free-form text such as
 * emails, delivery manifests, or rows copied from a spreadsheet, and skips
 * everything else (names, phone numbers, order numbers, prices, notes).
 *
 * Handles addresses on one line ("123 Main St, Springfield, IL 62704") or
 * split across lines (street, then unit, then city/state/ZIP).
 */
object AddressExtractor {

    private val SUFFIXES = listOf(
        "Street", "Str", "St", "Avenue", "Ave", "Av", "Road", "Rd", "Boulevard", "Blvd",
        "Drive", "Dr", "Lane", "Ln", "Way", "Wy", "Court", "Ct", "Place", "Pl", "Plaza", "Plz",
        "Square", "Sq", "Terrace", "Ter", "Terr", "Trail", "Trl", "Parkway", "Pkwy", "Pky",
        "Highway", "Hwy", "Circle", "Cir", "Loop", "Alley", "Aly", "Crescent", "Cres",
        "Grove", "Grv", "Path", "Pike", "Point", "Pt", "Expressway", "Expy",
        "Freeway", "Fwy", "Turnpike", "Tpke", "Causeway", "Cswy", "Center", "Ctr", "Crossing",
        "Xing", "Heights", "Hts", "Hill", "Ridge", "Rdg", "Landing", "Lndg", "Commons", "Walk",
        "Bend", "Cove", "Cv", "Glen", "Gln", "Mews", "Estates", "Est", "Village", "Vlg",
    ).joinToString("|")

    private val STATE_CODES = listOf(
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA", "HI", "ID", "IL", "IN", "IA",
        "KS", "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
        "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC", "SD", "TN", "TX", "UT", "VT",
        "VA", "WA", "WV", "WI", "WY", "DC", "PR", "VI", "GU",
        // Canadian provinces and territories
        "AB", "BC", "MB", "NB", "NL", "NS", "NT", "NU", "ON", "PE", "QC", "SK", "YT",
    ).joinToString("|")

    private val STATE_NAMES = listOf(
        "Alabama", "Alaska", "Arizona", "Arkansas", "California", "Colorado", "Connecticut",
        "Delaware", "District of Columbia", "Florida", "Georgia", "Hawaii", "Idaho", "Illinois",
        "Indiana", "Iowa", "Kansas", "Kentucky", "Louisiana", "Maine", "Maryland",
        "Massachusetts", "Michigan", "Minnesota", "Mississippi", "Missouri", "Montana",
        "Nebraska", "Nevada", "New Hampshire", "New Jersey", "New Mexico", "New York",
        "North Carolina", "North Dakota", "Ohio", "Oklahoma", "Oregon", "Pennsylvania",
        "Rhode Island", "South Carolina", "South Dakota", "Tennessee", "Texas", "Utah",
        "Vermont", "Virginia", "Washington", "West Virginia", "Wisconsin", "Wyoming",
        "Ontario", "Quebec", "British Columbia", "Alberta", "Manitoba", "Saskatchewan",
        "Nova Scotia", "New Brunswick",
    ).joinToString("|")

    /** US state codes that are also everyday words, so lowercase "in", "me", "or" aren't read as states. */
    private val WORD_LIKE_CODES = setOf(
        "AL", "AR", "CO", "DE", "GA", "HI", "ID", "IN", "LA", "MA", "ME", "MI", "MO", "NE", "OH",
        "OK", "OR", "PA", "VA", "WA", "AB", "BC", "MB", "NB", "NL", "NS", "NT", "NU", "ON", "PE",
        "QC", "SK", "YT", "VI", "GU", "PR",
    )
    private val SAFE_LOWERCASE_CODES =
        STATE_CODES.split("|").filter { it !in WORD_LIKE_CODES }.joinToString("|") { it.lowercase() }
    private val CAPITALIZED_CODES =
        STATE_CODES.split("|").joinToString("|") { it[0] + it.substring(1).lowercase() }

    /**
     * Words that are never part of a street or town name. Cuts false hits from
     * notes like "2 dogs on Main St" or "501-3 rows".
     */
    private const val STOP_WORDS =
        "on|at|in|to|of|the|and|or|for|from|near|by|my|your|our|is|are|was|were|be|have|has|had|" +
            "with|items?|boxes|box|pcs|pieces|packages?|units?|days?|hours?|hrs|minutes?|mins|times|" +
            "people|dollars|miles|mi|km|am|pm|rows?|cases?|bags?|oz|lbs?|cans?|bottles?|cartons?|" +
            "pallets?|shelf|shelves|feet|foot|ft|inch(?:es)?|sections?|aisles?|racks?|displays?"

    private const val ZIP = """(?-i:\d{5}(?:-\d{4})?|[A-Z]\d[A-Z] ?\d[A-Z]\d)"""

    // With a comma or ZIP: uppercase codes ("IL") or full names.
    private val STATE = """\b(?:(?-i:(?:$STATE_CODES))|(?:$STATE_NAMES))\b"""

    // Any case ("il"); only trusted when a ZIP code follows.
    private val STATE_ANY_CASE = """\b(?:$STATE_CODES|$STATE_NAMES)\b"""

    // No comma or ZIP ("willimantic ct"): capitalized codes, or lowercase codes that aren't words.
    private val STATE_LOOSE =
        """\b(?:(?-i:$STATE_CODES|$CAPITALIZED_CODES|$SAFE_LOWERCASE_CODES)|(?:$STATE_NAMES))\b"""

    private val CITY_WORD = """(?!(?:$STOP_WORDS)\b)[A-Za-z][A-Za-z.'\-]*"""
    private val CITY = """$CITY_WORD(?: $CITY_WORD){0,3}"""

    // Always at least a space or comma between parts, so names can't split mid-word.
    private const val SEP = """(?: ?, ?| )"""

    // "City, ST", "City, ST 12345", "City ST 12345", or "City ST" at the end of a phrase.
    private val LOCALITY_STRICT =
        """(?:$SEP$CITY$SEP$STATE_ANY_CASE$SEP?$ZIP|$SEP$CITY ?, ?$STATE(?:$SEP$ZIP)?)(?![\w-])"""
    private val LOCALITY = """(?:$LOCALITY_STRICT|$SEP$CITY $STATE_LOOSE(?![\w-]))"""

    private const val NUMBER = """(?<![\w#$.\-/:@])\d{1,6}[A-Za-z]?(?![\w-])"""
    private val WORD = """(?!(?:$STOP_WORDS)\b)[A-Za-z0-9][A-Za-z0-9'.\-]*"""
    private const val POST_DIRECTION = """(?: (?:N|S|E|W|NE|NW|SE|SW|North|South|East|West)\b\.?)?"""
    private const val UNIT =
        """(?:$SEP(?:Apt|Apartment|Suite|Ste|Unit|Bldg|Building|Floor|Fl|Room|Rm|Lot|Space|Spc)\b\.? ?#? ?[A-Za-z0-9\-]+|$SEP# ?[A-Za-z0-9\-]+)?"""
    private const val ROUTE_NAMES =
        """(?:Highway|Hwy|Route|Rte|State Route|State Road|County Road|County Rd|Farm to Market|(?-i:US|SR|CR|FM))"""

    /** "123 Main St" or "4500 Hwy 50", with an optional city/state. */
    private val WITH_SUFFIX = Regex(
        "$NUMBER (?:(?:$WORD ){1,5}?(?:$SUFFIXES)\\b\\.?|(?:$WORD ){0,2}?$ROUTE_NAMES ?-? ?\\d{1,4}[A-Za-z]?\\b)" +
            "$POST_DIRECTION$UNIT(?<loc>$LOCALITY)?",
        RegexOption.IGNORE_CASE,
    )

    /** Street names without a suffix ("350 Broadway") count only when a city and state follow. */
    private val WITH_LOCALITY = Regex(
        "$NUMBER (?:$WORD ){0,3}?$WORD$UNIT$LOCALITY_STRICT",
        RegexOption.IGNORE_CASE,
    )

    /** Returns the addresses found in [text], in the order they appear, without duplicates. */
    fun extract(text: String): List<String> {
        val flat = flatten(text)
        val matches = (WITH_SUFFIX.findAll(flat) + WITH_LOCALITY.findAll(flat))
            .map { it.range }
            .sortedWith(compareBy<IntRange> { it.first }.thenByDescending { it.last })
            .toList()

        // Keep the longest match where several overlap.
        val chosen = mutableListOf<IntRange>()
        for (range in matches) {
            val overlapping = chosen.lastOrNull()?.takeIf { it.last >= range.first }
            if (overlapping == null) {
                chosen.add(range)
            } else if (range.last - range.first > overlapping.last - overlapping.first) {
                chosen[chosen.size - 1] = range
            }
        }

        val seen = HashSet<String>()
        return chosen.map { clean(flat.substring(it)) }
            .filter { it.isNotEmpty() && seen.add(key(it)) }
    }

    /** True if the address names a city/state or ZIP, so it can be looked up anywhere. */
    fun hasLocality(address: String): Boolean {
        val flat = flatten(address)
        if (WITH_LOCALITY.containsMatchIn(flat)) return true
        val matcher = WITH_SUFFIX.toPattern().matcher(flat)
        while (matcher.find()) {
            if (matcher.group("loc") != null) return true
        }
        return false
    }

    /** Comparison key that ignores case, punctuation, and spacing. */
    fun key(address: String): String = address.lowercase().replace(Regex("[^a-z0-9]"), "")

    /** Puts everything on one line so addresses split across lines join back up. */
    private fun flatten(text: String): String = text
        .replace('\u00A0', ' ')
        .replace(Regex("""[\t\r\n|;•]+"""), ", ")
        .replace(Regex(""" {2,}"""), " ")
        .replace(Regex("""\s*,(?:\s*,)+"""), ",")
        .replace(Regex(""" ?, ?"""), ", ")

    private fun clean(address: String): String = address
        .trim()
        .trim(',', ' ')
        .replace(Regex("""\s+"""), " ")
}
