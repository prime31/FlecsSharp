﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Flecs
{
	public struct MemoryPage : IDisposable
	{
		public const int PageSize = 8192;
		IntPtr ptr;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MemoryPage Create()
		{
			MemoryPage page;
			page.ptr = Heap.Alloc(PageSize);
			return page;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Dispose() => Heap.Free(ptr);
	}

	public unsafe struct DynamicBuffer : IDisposable
	{
		Header* header;
		private struct Header
		{
			internal int size;
			internal int currentOffset;
			internal int start;
		}

		const int NATURAL_ALIGNMENT = 16;
		private DynamicBuffer(Header* header) => this.header = header;
		public static DynamicBuffer Create(int size = 4096)
		{
			Header* mem = (Header*)Heap.Alloc(size);
			mem->size = size;

			var offset = Marshal.SizeOf<Header>();
			offset += offset % NATURAL_ALIGNMENT;
			mem->currentOffset = offset;
			mem->start = offset;
			return new DynamicBuffer(mem);
		}

		public void Dispose() => Heap.Free(header);


		byte* thisPtr => (byte*)header;
		byte* bufferEnd => thisPtr + header->size;

		byte* _aligned(int alignment, bool apply = false)
		{
			unchecked
			{
				var current = thisPtr + header->currentOffset;
				current += ((long)current) % alignment;

				if (apply)
					header->currentOffset = (int)(current - thisPtr);

				return current;
			}
		}

		Span<byte> GetAvailableSpan(int align = NATURAL_ALIGNMENT)
		{
			unchecked
			{
				var aligned = _aligned(align);
				var sizeLeft = header->size - (int)(aligned - thisPtr);
				Console.WriteLine($"--- {sizeLeft}");
				return new Span<byte>(aligned, (int)sizeLeft);
			}
		}

		public Span<byte> AsSpan()
		{
			unchecked
			{
				var startPtr = thisPtr + header->start;
				var offset = header->currentOffset - header->start;
				return new Span<byte>(startPtr, offset);
			}
		}

		public IntPtr Acquire(int size, int align = NATURAL_ALIGNMENT)
		{
			unchecked
			{
				var ptr = _aligned(align, true);
				var nextPtr = ptr + size;
				var newOffset = (int)(nextPtr - thisPtr);

				Console.WriteLine($"req: {size}, offset: {header->currentOffset}, newOffset: {newOffset}, tot: {header->size}, space: " + ((nextPtr >= bufferEnd) ? "no" : "yes"));
				header->currentOffset = newOffset;

				if (nextPtr >= bufferEnd)
				{
					var newSize = (int)(newOffset * 1.3F); // grow by 30%
					newSize += newOffset % 4096; // align to a pretty number
					header = (Header*)Heap.Realloc(header, newSize);
					header->size = newSize;
				}

				return (IntPtr)ptr;
			}
		}

		public CharPtr AddUTF8String(ReadOnlySpan<char> str) => AddString(str, Encoding.UTF8);

		public CharPtr AddASCIIString(ReadOnlySpan<char> str) => AddString(str, Encoding.ASCII);

		public CharPtr AddString(ReadOnlySpan<char> str, Encoding encoding)
		{
			const int STR_ALIGN = 8; // 8 bytes alignment for string would be better to compute hashes

			var available = GetAvailableSpan(STR_ALIGN);
			if (available.Length < str.Length)
			{
				Console.WriteLine("-----fucked");
				var newSize = (int)(header->size * 1.3F); // grow by 30%
				newSize += newSize % 4096; // align to a pretty number
				header = (Header*)Heap.Realloc(header, newSize);
				header->size = newSize;

				available = GetAvailableSpan(STR_ALIGN);
			}

			var len = encoding.GetBytes(str, available);
			available[len] = 0; // null terminated
			var charPtr = Acquire(len + 1, STR_ALIGN);
			return (CharPtr)charPtr;
		}

		public override string ToString() => System.Text.Encoding.UTF8.GetString(AsSpan());
	}

	public unsafe static class Heap
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntPtr Alloc(int size) => Marshal.AllocHGlobal((int)size);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T* Alloc<T>(T defaultVal = default) where T : unmanaged
		{
			var ptr = (T*)Alloc(Marshal.SizeOf<T>());
			*ptr = defaultVal;
			return ptr;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Free<T>(T* ptr) where T : unmanaged => Marshal.FreeHGlobal((IntPtr)ptr);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Free(IntPtr ptr) => Marshal.FreeHGlobal((IntPtr)ptr);

		internal static void* Realloc(void* ptr, int newSize)
			=> (void*)Marshal.ReAllocHGlobal((IntPtr)ptr, (IntPtr)newSize);
	}
}
