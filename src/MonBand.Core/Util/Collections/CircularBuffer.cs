﻿#region License
// Adapted from: https://github.com/joaoportela/CircullarBuffer-CSharp
/*---------------------------------------------------------------------------
"THE BEER-WARE LICENSE" (Revision 42):
Joao Portela wrote this file. As long as you retain this notice you
can do whatever you want with this stuff. If we meet some day, and you think
this stuff is worth it, you can buy me a beer in return.
Joao Portela
---------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections;
using System.Collections.Generic;

namespace MonBand.Core.Util.Collections
{
    /// <summary>
    /// Circular buffer.
    /// 
    /// When writing to a full buffer:
    /// PushBack -> removes this[0] / Front()
    /// PushFront -> removes this[Size-1] / Back()
    /// 
    /// this implementation is inspired by
    /// http://www.boost.org/doc/libs/1_53_0/libs/circular_buffer/doc/circular_buffer.html
    /// because I liked their interface.
    /// </summary>
    class CircularBuffer<T> : IReadOnlyList<T>
    {
        readonly T[] _buffer;

        int _start;
        int _end;

        /// <summary>
        /// Maximum capacity of the buffer. Elements pushed into the buffer after
        /// maximum capacity is reached (IsFull = true), will remove an element.
        /// </summary>
        public int Capacity => this._buffer.Length;

        public bool IsFull => this.Count == this.Capacity;

        public bool IsEmpty => this.Count == 0;

        /// <summary>
        /// Current buffer size (the number of elements that the buffer has).
        /// </summary>
        public int Count { get; private set; }

        public CircularBuffer(int capacity) : this(capacity, new T[] { }) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularBuffer{T}"/> class.
        /// 
        /// </summary>
        /// <param name='capacity'>
        /// Buffer capacity. Must be positive.
        /// </param>
        /// <param name='items'>
        /// Items to fill buffer with. Items length must be less than capacity.
        /// Suggestion: use Skip(x).Take(y).ToArray() to build this argument from
        /// any enumerable.
        /// </param>
        public CircularBuffer(int capacity, T[] items)
        {
            if (capacity < 1)
            {
                throw new ArgumentException(
                    "Circular buffer cannot have negative or zero capacity.",
                    nameof(capacity));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items.Length > capacity)
            {
                throw new ArgumentException(
                    "Too many items to fit circular buffer",
                    nameof(items));
            }

            this._buffer = new T[capacity];

            Array.Copy(items, this._buffer, items.Length);
            this.Count = items.Length;

            this._start = 0;
            this._end = this.Count == capacity ? 0 : this.Count;
        }

        /// <summary>
        /// Element at the front of the buffer - this[0].
        /// </summary>
        /// <returns>The value of the element of type T at the front of the buffer.</returns>
        public T Front()
        {
            this.ThrowIfEmpty();
            return this._buffer[this._start];
        }

        /// <summary>
        /// Element at the back of the buffer - this[Size - 1].
        /// </summary>
        /// <returns>The value of the element of type T at the back of the buffer.</returns>
        public T Back()
        {
            this.ThrowIfEmpty();
            return this._buffer[(this._end != 0 ? this._end : this.Capacity) - 1];
        }

        public T this[int index]
        {
            get
            {
                if (this.IsEmpty)
                {
                    throw new IndexOutOfRangeException($"Cannot access index {index}. Buffer is empty");
                }

                if (index >= this.Count)
                {
                    throw new IndexOutOfRangeException($"Cannot access index {index}. Buffer size is {this.Count}");
                }

                int actualIndex = this.InternalIndex(index);
                return this._buffer[actualIndex];
            }
            set
            {
                if (this.IsEmpty)
                {
                    throw new IndexOutOfRangeException($"Cannot access index {index}. Buffer is empty");
                }

                if (index >= this.Count)
                {
                    throw new IndexOutOfRangeException($"Cannot access index {index}. Buffer size is {this.Count}");
                }

                int actualIndex = this.InternalIndex(index);
                this._buffer[actualIndex] = value;
            }
        }

        /// <summary>
        /// Pushes a new element to the back of the buffer. Back()/this[Size-1]
        /// will now return this element.
        /// 
        /// When the buffer is full, the element at Front()/this[0] will be 
        /// popped to allow for this new element to fit.
        /// </summary>
        /// <param name="item">Item to push to the back of the buffer</param>
        public void PushBack(T item)
        {
            if (this.IsFull)
            {
                this._buffer[this._end] = item;
                this.Increment(ref this._end);
                this._start = this._end;
            }
            else
            {
                this._buffer[this._end] = item;
                this.Increment(ref this._end);
                ++this.Count;
            }
        }

        /// <summary>
        /// Pushes a new element to the front of the buffer. Front()/this[0]
        /// will now return this element.
        /// 
        /// When the buffer is full, the element at Back()/this[Size-1] will be 
        /// popped to allow for this new element to fit.
        /// </summary>
        /// <param name="item">Item to push to the front of the buffer</param>
        public void PushFront(T item)
        {
            if (this.IsFull)
            {
                this.Decrement(ref this._start);
                this._end = this._start;
                this._buffer[this._start] = item;
            }
            else
            {
                this.Decrement(ref this._start);
                this._buffer[this._start] = item;
                ++this.Count;
            }
        }

        /// <summary>
        /// Removes the element at the back of the buffer. Decreasing the 
        /// Buffer size by 1.
        /// </summary>
        public void PopBack()
        {
            this.ThrowIfEmpty("Cannot take elements from an empty buffer.");
            this.Decrement(ref this._end);
            this._buffer[this._end] = default(T);
            --this.Count;
        }

        /// <summary>
        /// Removes the element at the front of the buffer. Decreasing the 
        /// Buffer size by 1.
        /// </summary>
        public void PopFront()
        {
            this.ThrowIfEmpty("Cannot take elements from an empty buffer.");
            this._buffer[this._start] = default(T);
            this.Increment(ref this._start);
            --this.Count;
        }
        
        void ThrowIfEmpty(string message = "Cannot access an empty buffer.")
        {
            if (this.IsEmpty)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Increments the provided index variable by one, wrapping
        /// around if necessary.
        /// </summary>
        /// <param name="index"></param>
        void Increment(ref int index)
        {
            if (++index == this.Capacity)
            {
                index = 0;
            }
        }

        /// <summary>
        /// Decrements the provided index variable by one, wrapping
        /// around if necessary.
        /// </summary>
        /// <param name="index"></param>
        void Decrement(ref int index)
        {
            if (index == 0)
            {
                index = this.Capacity;
            }

            index--;
        }

        /// <summary>
        /// Converts the index in the argument to an index in <code>_buffer</code>
        /// </summary>
        /// <returns>
        /// The transformed index.
        /// </returns>
        /// <param name='index'>
        /// External index.
        /// </param>
        int InternalIndex(int index)
        {
            return this._start + (index < (this.Capacity - this._start) ? index : index - this.Capacity);
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }

        public struct Enumerator : IEnumerator<T>
        {
            readonly CircularBuffer<T> _buffer;
            int _index;

            object IEnumerator.Current => this.Current;

            public T Current => this._buffer[this._index];

            internal Enumerator(CircularBuffer<T> buffer)
            {
                this._buffer = buffer;
                this._index = 0;
            }

            public bool MoveNext() => ++this._index < this._buffer.Count;

            public void Reset() => throw new NotSupportedException();

            public void Dispose() { }
        }
    }
}
