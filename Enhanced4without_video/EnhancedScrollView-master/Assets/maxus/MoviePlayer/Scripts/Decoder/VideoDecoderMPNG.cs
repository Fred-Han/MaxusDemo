//--------------------------------------------
// Movie Player
// Copyright © 2014-2015 SHUU Games
//--------------------------------------------
using UnityEngine;

namespace MP.Decoder
{
	/// <summary>
	/// Video decoder for MPNG stream
	/// </summary>
	public class VideoDecoderMPNG : VideoDecoderUnity
	{
		#region ----- Constants -----

		public const uint FOURCC_MPNG = 0x474E504D;

		#endregion

		#region ----- Public methods and properties -----

		/// <summary>
		/// Constructor. It's always reated for a stream, so you need to provide info about it here.
		/// </summary>
		public VideoDecoderMPNG (VideoStreamInfo streamInfo = null) : base(streamInfo)
		{
		}

		#endregion

		#region ----- Private members -----

		// used to check if framebuffer dimensions change from frame to frame
		protected int lastFbWidth = -1, lastFbHeight = -1;

		public override void DecodeNext ()
		{
			// for safety
			if (framebuffer == null)
				return;
			
			// start the stopwatch
			watch.Reset ();
			watch.Start ();
			
			// read frame data from the steam
			byte[] buf;
			int bytesRead = demux.ReadVideoFrame (out buf);
			
			// Decode the frame. Since it's actually JPEG or PNG, Unity's
			// LoadImage method can load it pretty fast. Maybe 15ms per 720p frame.
			// Unfortunately this method is a bit buggy. It won't return FALSE as
			// documentation say if the buf contains invalid data.
			bool success = framebuffer.LoadImage (buf);
			
			// Double check if the image contruction failed. We're doing it by checking
			// wether frame dimensions change or not (they shouldn't).
			if (success && lastFbWidth > 0) {
				if (framebuffer.width != lastFbWidth || framebuffer.height != lastFbHeight) {
					success = false;
				}
				lastFbWidth = framebuffer.width;
				lastFbHeight = framebuffer.height;
			}
			
			// only upload the texture to GPU if LoadImage went well
			if (success) {
				framebuffer.Apply (false);
			} else {
				// not using "#if MP_DEBUG" here, because you want to know about it!
				Debug.LogError ("Couldn't decode frame " + (demux.VideoPosition - 1) + " from " + buf.Length + " bytes");
			}
			
			// register frame decode time
			watch.Stop ();
			_lastFrameDecodeTime = (float)(0.001f * watch.Elapsed.TotalMilliseconds);
			_lastFrameSizeBytes = bytesRead;
			_totalDecodeTime += _lastFrameDecodeTime;
			_totalSizeBytes += _lastFrameSizeBytes;
		}

		#endregion
	}

}
