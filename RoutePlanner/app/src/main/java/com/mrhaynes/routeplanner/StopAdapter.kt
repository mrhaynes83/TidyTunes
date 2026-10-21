package com.mrhaynes.routeplanner

import android.annotation.SuppressLint
import android.graphics.Paint
import android.view.LayoutInflater
import android.view.MotionEvent
import android.view.View
import android.view.ViewGroup
import android.widget.CheckBox
import android.widget.ImageButton
import android.widget.ImageView
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView

class StopAdapter(
    private val stops: MutableList<Stop>,
    private val listener: Listener,
) : RecyclerView.Adapter<StopAdapter.Holder>() {

    interface Listener {
        fun onStopClicked(position: Int)
        fun onDoneChanged(position: Int, done: Boolean)
        fun onDeleteClicked(position: Int)
        fun onStartDrag(holder: RecyclerView.ViewHolder)
    }

    class Holder(view: View) : RecyclerView.ViewHolder(view) {
        val number: TextView = view.findViewById(R.id.stop_number)
        val label: TextView = view.findViewById(R.id.stop_label)
        val done: CheckBox = view.findViewById(R.id.stop_done)
        val delete: ImageButton = view.findViewById(R.id.stop_delete)
        val handle: ImageView = view.findViewById(R.id.stop_handle)
    }

    override fun getItemCount() = stops.size

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): Holder =
        Holder(LayoutInflater.from(parent.context).inflate(R.layout.item_stop, parent, false))

    @SuppressLint("ClickableViewAccessibility")
    override fun onBindViewHolder(holder: Holder, position: Int) {
        val stop = stops[position]
        holder.number.text = (position + 1).toString()
        holder.number.isEnabled = !stop.done
        holder.label.text = stop.label
        holder.label.paintFlags = if (stop.done) {
            holder.label.paintFlags or Paint.STRIKE_THRU_TEXT_FLAG
        } else {
            holder.label.paintFlags and Paint.STRIKE_THRU_TEXT_FLAG.inv()
        }
        holder.label.alpha = if (stop.done) 0.5f else 1f

        holder.done.setOnCheckedChangeListener(null)
        holder.done.isChecked = stop.done
        holder.done.setOnCheckedChangeListener { _, checked ->
            val pos = holder.bindingAdapterPosition
            if (pos != RecyclerView.NO_POSITION) listener.onDoneChanged(pos, checked)
        }
        holder.delete.setOnClickListener {
            val pos = holder.bindingAdapterPosition
            if (pos != RecyclerView.NO_POSITION) listener.onDeleteClicked(pos)
        }
        holder.itemView.setOnClickListener {
            val pos = holder.bindingAdapterPosition
            if (pos != RecyclerView.NO_POSITION) listener.onStopClicked(pos)
        }
        holder.handle.setOnTouchListener { _, event ->
            if (event.actionMasked == MotionEvent.ACTION_DOWN) listener.onStartDrag(holder)
            false
        }
    }
}
