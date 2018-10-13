//--------------------------------------------
// Movie Player
// Copyright © 2014-2015 SHUU Games
//--------------------------------------------
using UnityEngine;

namespace MP.Decoder
{
	/// <summary>
	/// Video decoder for MJPEG stream
	/// </summary>
	public class VideoDecoderMJPEG : VideoDecoderUnity
	{
		#region ----- Constants -----

		public const uint FOURCC_MJPG = 0x47504A4D;
		public const uint FOURCC_CJPG = 0x47504A43;
		public const uint FOURCC_ffds = 0x73646666; // not tested!
		public const uint FOURCC_jpeg = 0x6765706A;

		#endregion

		#region ----- Public methods and properties -----

		/// <summary>
		/// Constructor. It's always reated for a stream, so you need to provide info about it here.
		/// </summary>
		public VideoDecoderMJPEG (VideoStreamInfo streamInfo = null) : base(streamInfo)
		{
		}

		/// <summary>
		/// If the video is interlaced and field order is wrong, then change this to true
		/// </summary>
		public bool interlacedEvenFieldIsLower = false;

		#endregion

		#region ----- Private members -----

		Texture2D field = null; // lazy initalized

		// used to check if framebuffer dimensions change from frame to frame
		protected int lastFbWidth = -1, lastFbHeight = -1;

		public override void Shutdown ()
		{
			base.Shutdown ();

			if (field != null) {
				if (Application.isEditor) {
					Texture2D.DestroyImmediate (field);
				} else {
					Texture2D.Destroy (field);
				}
			}
		}

		int FindMarker(byte[] buf, int bufSize, byte marker)
		{
			for(int i = 1; i < bufSize; i++) {
				if(buf[i - 1] == 0xFF && buf[i] == marker) return i - 1;
			}
			return -1; // not found
		}

		// NB! modifies buf contents, buf needs to have extra 420B of space at the end for DHT
		bool DecodeField(byte[] buf, int offset, int size, bool needToAddDHT, bool evenField)
		{
			// Copy bytes so that we can use LoadImage on buf
			if (offset != 0) {
				System.Array.Copy(buf, offset, buf, 0, size);
			}

			if (needToAddDHT)
			{
				// Assuming actual buf size is 420B larger, it should be allocated in Debux!
				if (size > buf.Length - mjpegDefaultDHT.Length) {
					Debug.LogError ("Demux didn't allocate extra "+mjpegDefaultDHT.Length+"B for adding DHT, but we need it (a bug in Demux class)");
					return false;
				}

				// Find where to insert MJPEG DHT, let it be right before SOS marker.
				int sosPos = FindMarker (buf, size, 0xDA);
				if (sosPos >= 0)
				{
					// make room for huffman table and then copy it over, it includes marker bytes too
					System.Array.Copy (buf, sosPos, buf, sosPos + mjpegDefaultDHT.Length, size - sosPos);
					System.Array.Copy (mjpegDefaultDHT, 0, buf, sosPos, mjpegDefaultDHT.Length);
				}
			}

			// Actually decode the field
			bool success = field.LoadImage (buf);
			if (success)
			{
				// Framebuffer size is determined by field size for interlaced video. Resize if necessary
				if(framebuffer.width != field.width || framebuffer.height != field.height * 2) {
					framebuffer.Resize (field.width, field.height * 2, field.format, false);
				}

				// Copy scanlines over from field to framebuffer texture
				// (tested 3.8ms for 624x468px, but it's GC intensive!)
				int yOffset = evenField ^ interlacedEvenFieldIsLower ? 1 : 0;
				for(int y = 0; y < framebuffer.height / 2; y++)
				{
					var pixels = field.GetPixels(0, y, framebuffer.width, 1);
					framebuffer.SetPixels(0, y * 2 + yOffset, framebuffer.width, 1, pixels);
				}
			}
			return success;
		}

		public override void DecodeNext ()
		{
			// For safety
			if (framebuffer == null)
				return;
			
			// Start the stopwatch
			watch.Reset ();
			watch.Start ();
			
			// Read frame data from the steam
			byte[] buf;
			int bytesRead = demux.ReadVideoFrame (out buf);

			// If there's no video content in this frame, ignore it
			if(bytesRead > 0)
			{
				// Detect if we've got MJPEG frame that Unity can't readily decode.
				// If APP0 marker states video identifier to be AVI1 we need to add DHT ourself
				// and we also need to detect if it's interlaced frame or not.
				int app0Pos = FindMarker (buf, bytesRead, 0xE0);
				bool needToAddDHT = app0Pos >= 0 &&
						buf [app0Pos + 4] == 'A' &&
						buf [app0Pos + 5] == 'V' &&
						buf [app0Pos + 6] == 'I' &&
						buf [app0Pos + 7] == '1';
				bool interleaved = needToAddDHT && buf [app0Pos + 8] != 0;

				bool success;

				if (interleaved)
				{
					// For interlaced video we need to know if it's even or odd field.
					// Sometimes video index will point to even-odd video field pair.
					// If it's a pair, automatically assume the second field is !evenField.
					bool evenField = buf [app0Pos + 8] == 2;

					// For interleaved video we need another texture object to temporarily store field info.
					if (field == null) field = new Texture2D (4, 4, TextureFormat.RGB24, false);

					// Decode field
					success = DecodeField(buf, 0, bytesRead, needToAddDHT, evenField);
					if(success)
					{
						// Detect if there is indeed a second field
						int secondFieldPos = -1;
						int secondFieldSize = 0;

						// If the last DecodeField needed to add DHT to buffer, then effective number
						// of bytes in but increased by DHT size
						if(needToAddDHT) bytesRead += mjpegDefaultDHT.Length;

						int endPos = FindMarker(buf, bytesRead, 0xD9);
						if(bytesRead > endPos + 6)
						{
							if(buf[endPos + 2] == 0xFF && buf[endPos + 3] == 0xD8) secondFieldPos = endPos + 2;
							if(buf[endPos + 4] == 0xFF && buf[endPos + 5] == 0xD8) secondFieldPos = endPos + 4;
						}
						secondFieldSize = bytesRead - secondFieldPos;

						// Decode second field
						// For the second field we're not detecting if it needs DHT to be added or
						// if it is even or odd, because it can be assumed rather safely from the first field.
						if(secondFieldPos > 0) {
							success = DecodeField(buf, secondFieldPos, secondFieldSize, needToAddDHT, !evenField);
						}
					}
				}
				// not interleaved
				else {
					// Decode the frame. Since it's actually JPEG, Unity's
					// LoadImage method can load it pretty fast. Maybe 15ms per 720p frame.
					// Unfortunately this method is a bit buggy. It won't return FALSE as
					// documentation say if the buf contains invalid data.
					success = framebuffer.LoadImage (buf);

					// Double check if the image contruction failed. We're doing it by checking
					// wether frame dimensions change or not (they shouldn't).
					if (success && lastFbWidth > 0) {
						if (framebuffer.width != lastFbWidth || framebuffer.height != lastFbHeight) {
							success = false;
						}
						lastFbWidth = framebuffer.width;
						lastFbHeight = framebuffer.height;
					}
				}

				// Only upload the texture to GPU if decoding went well
				if (success) {
					framebuffer.Apply (false);
				}
				else {
					// not using "#if MP_DEBUG" here, because you want to know about it!
					Debug.LogError ("Couldn't decode frame " + (demux.VideoPosition - 1) + " from " + buf.Length + " bytes");
				}
			}
			
			// register frame decode time
			watch.Stop ();
			_lastFrameDecodeTime = (float)(0.001f * watch.Elapsed.TotalMilliseconds);
			_lastFrameSizeBytes = bytesRead;
			_totalDecodeTime += _lastFrameDecodeTime;
			_totalSizeBytes += _lastFrameSizeBytes;
		}

		/// <summary>
		/// If the jpeg frame doesn't contain DHT then this is used. It includes a JPEG marker. Size is 420B with marker
		/// </summary>
		static byte[] mjpegDefaultDHT = new byte[]
		{
			0xff, 0xc4, 0x01, 0xa2, 0x00, 0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a,
			0x0b, 0x01, 0x00, 0x03, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x10, 0x00,
			0x02, 0x01, 0x03, 0x03, 0x02, 0x04, 0x03, 0x05, 0x05, 0x04, 0x04, 0x00, 0x00, 0x01, 0x7d, 0x01,
			0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07, 0x22,
			0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08, 0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0, 0x24,
			0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28, 0x29,
			0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a,
			0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a,
			0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a,
			0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8,
			0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6,
			0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2, 0xe3,
			0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9,
			0xfa, 0x11, 0x00, 0x02, 0x01, 0x02, 0x04, 0x04, 0x03, 0x04, 0x07, 0x05, 0x04, 0x04, 0x00, 0x01,
			0x02, 0x77, 0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07,
			0x61, 0x71, 0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33,
			0x52, 0xf0, 0x15, 0x62, 0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34, 0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19,
			0x1a, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46,
			0x47, 0x48, 0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x63, 0x64, 0x65, 0x66,
			0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x82, 0x83, 0x84, 0x85,
			0x86, 0x87, 0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3,
			0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba,
			0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8,
			0xd9, 0xda, 0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6,
			0xf7, 0xf8, 0xf9, 0xfa
		};

		#endregion
	}

}
