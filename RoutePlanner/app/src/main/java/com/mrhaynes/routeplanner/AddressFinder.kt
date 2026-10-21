package com.mrhaynes.routeplanner

import android.content.Context
import android.os.Build
import android.view.textclassifier.TextClassificationManager
import android.view.textclassifier.TextClassifier
import android.view.textclassifier.TextLinks
import androidx.annotation.RequiresApi
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Finds addresses in pasted text. Uses [AddressExtractor] first, then adds
 * anything extra that Android's built-in text classifier recognizes as an
 * address (helps with formats the extractor doesn't know).
 */
object AddressFinder {

    suspend fun find(context: Context, text: String): List<String> = withContext(Dispatchers.Default) {
        val found = AddressExtractor.extract(text).toMutableList()
        if (Build.VERSION.SDK_INT >= 28) {
            val keys = found.map { AddressExtractor.key(it) }.toMutableList()
            for (extra in classifierAddresses(context, text)) {
                val key = AddressExtractor.key(extra)
                val known = keys.any { it.contains(key) || key.contains(it) }
                if (!known && key.length >= 8 && extra.any { it.isDigit() }) {
                    found.add(extra)
                    keys.add(key)
                }
            }
        }
        found
    }

    @RequiresApi(28)
    @Suppress("DEPRECATION")
    private fun classifierAddresses(context: Context, text: String): List<String> {
        return try {
            val manager = context.getSystemService(TextClassificationManager::class.java)
                ?: return emptyList()
            val classifier = manager.textClassifier
            if (classifier === TextClassifier.NO_OP) return emptyList()
            val input = text.take(classifier.maxGenerateLinksTextLength)
            val request = TextLinks.Request.Builder(input)
                .setEntityConfig(
                    TextClassifier.EntityConfig.createWithExplicitEntityList(listOf(TextClassifier.TYPE_ADDRESS)),
                )
                .build()
            classifier.generateLinks(request).links
                .filter { it.getConfidenceScore(TextClassifier.TYPE_ADDRESS) >= 0.5f }
                .map { input.substring(it.start, it.end).replace(Regex("\\s+"), " ").trim(' ', ',') }
                .filter { it.isNotEmpty() }
        } catch (e: Exception) {
            // The classifier is optional; fall back to the extractor's results.
            emptyList()
        }
    }
}
