package com.mrhaynes.routeplanner

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class AddressExtractorTest {

    @Test
    fun findsAddressesInAnEmailAndSkipsTheRest() {
        val text = """
            Hi Sean,

            Here are today's deliveries. Call me at 555-123-4567 if anything changes.
            Order #48213 - 3 boxes - ${'$'}45.99
            Deliver to: 123 Main St, Springfield, IL 62704 (leave at back door)

            Order #48214, due 10/21/2026
            Jane Doe
            4500 W Elm Avenue Apt 12
            Chicago, IL 60601
            Phone: (312) 555-0199

            Also stop by 1600 Pennsylvania Ave NW, Washington, DC 20500 before 5 pm.
            Thanks!
        """.trimIndent()

        assertEquals(
            listOf(
                "123 Main St, Springfield, IL 62704",
                "4500 W Elm Avenue Apt 12, Chicago, IL 60601",
                "1600 Pennsylvania Ave NW, Washington, DC 20500",
            ),
            AddressExtractor.extract(text),
        )
    }

    @Test
    fun readsSpreadsheetRows() {
        val text = "Name\tAddress\tCity\tState\tZip\tPhone\n" +
            "Bob Smith\t77 Oak Dr\tAustin\tTX\t78701\t512-555-0100\n" +
            "Ann Lee\t9 Pine Rd\tDallas\tTX\t75201\t214-555-0111\n"
        assertEquals(
            listOf("77 Oak Dr, Austin, TX, 78701", "9 Pine Rd, Dallas, TX, 75201"),
            AddressExtractor.extract(text),
        )
    }

    @Test
    fun acceptsAPlainListOfStreets() {
        val text = "123 main st\n45 Lake Shore Dr.\n200 Route 9\n12 5th Ave"
        assertEquals(
            listOf("123 main st", "45 Lake Shore Dr.", "200 Route 9", "12 5th Ave"),
            AddressExtractor.extract(text),
        )
    }

    @Test
    fun acceptsStreetsWithoutSuffixWhenCityAndStateFollow() {
        assertEquals(
            listOf("350 Broadway, New York, NY 10007"),
            AddressExtractor.extract("Office: 350 Broadway, New York, NY 10007."),
        )
    }

    @Test
    fun acceptsLowercaseStateWhenZipFollows() {
        assertEquals(
            listOf("18 n. maple ave. springfield il 62701"),
            AddressExtractor.extract("meet me at 4th and main at 3:30, then 18 n. maple ave. springfield il 62701"),
        )
    }

    @Test
    fun skipsPoBoxesButKeepsNearbyStreets() {
        val text = "1. Smith - 22 Birch Ln, Unit 5, Boise, ID 83702 - gate code 4411\n" +
            "2. PO Box 55, Reno NV 89501\n" +
            "3. Pickup at 1 Yonge St, Toronto, ON M5E 1E5"
        assertEquals(
            listOf("22 Birch Ln, Unit 5, Boise, ID 83702", "1 Yonge St, Toronto, ON M5E 1E5"),
            AddressExtractor.extract(text),
        )
    }

    @Test
    fun ignoresTextWithNoAddresses() {
        val text = """
            Meeting moved to 3 pm on 10/22. Bring 2 laptops and 15 copies.
            Invoice 2024-118 total ${'$'}1,250.00. Call 800-555-0123 or email a@b.com.
            We have 2 dogs on Main St duty today.
        """.trimIndent()
        assertEquals(emptyList<String>(), AddressExtractor.extract(text))
    }

    @Test
    fun removesDuplicates() {
        val text = "123 Main St, Springfield, IL\n123 MAIN ST,  Springfield, IL\n"
        assertEquals(listOf("123 Main St, Springfield, IL"), AddressExtractor.extract(text))
    }

    @Test
    fun handlesManyAddresses() {
        val text = (1..40).joinToString("\n") { "Stop $it: ${it * 10} Maple St, Denver, CO 80202 - note $it" }
        val found = AddressExtractor.extract(text)
        assertEquals(40, found.size)
        assertEquals("10 Maple St, Denver, CO 80202", found.first())
    }

    @Test
    fun knowsWhenAnAddressHasACity() {
        assertTrue(AddressExtractor.hasLocality("123 Main St, Springfield, IL"))
        assertTrue(AddressExtractor.hasLocality("77 Oak Dr Austin TX 78701"))
        assertFalse(AddressExtractor.hasLocality("123 Main St"))
    }
}
