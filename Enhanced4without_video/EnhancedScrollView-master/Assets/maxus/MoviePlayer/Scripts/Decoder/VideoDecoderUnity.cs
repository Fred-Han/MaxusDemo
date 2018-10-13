//--------------------------------------------
// Movie Player
// Copyright © 2014-2015 SHUU Games
//--------------------------------------------
using UnityEngine;

namespace MP.Decoder
{
	/// <summary>
	/// Video decoder that's built around Texture2D.LoadImage
	/// </summary>
	public abstract class VideoDecoderUnity : VideoDecoder
	{
		#region ----- Public methods and properties -----

		/// <summary>
		/// Constructor. It's always reated for a stream, so you need to provide info about it here.
		/// </summary>
		public VideoDecoderUnity (VideoStreamInfo streamInfo = null)
		{
			this.streamInfo = streamInfo;
		}

		/// <summary>
		/// Initializes the decoder for playing back given video stream. It returns a framebuffer
		/// which is updated with decoded frame pixel data.
		/// </summary>
		/// <param name="framebuffer">Framebuffer.</param>
		/// <param name="stream">Stream.</param>
		/// <param name="loadOptions">Load options.</param>
		public override void Init (out Texture2D framebuffer, Demux demux, LoadOptions loadOptions = null)
		{
			// can we decode this stream?
			if (demux == null) {
				throw new System.ArgumentException ("Missing Demux to get video frames from");
			}

			// create framebuffer and initialize vars. Texture size and format are not important here,
			// becase they'll be overwritten when a frame is decoded.
			this.framebuffer = new Texture2D (4, 4, TextureFormat.RGB24, false);
			framebuffer = this.framebuffer;
			this.demux = demux;

			this._lastFrameDecodeTime = 0;
			this._totalDecodeTime = 0;
			this.watch = new System.Diagnostics.Stopwatch ();
		}

		public override void Shutdown ()
		{
			if (framebuffer != null) {
				if (Application.isEditor) {
					Texture2D.DestroyImmediate (framebuffer);
				} else {
					Texture2D.Destroy (framebuffer);
				}
			}
		}

		public override int Position {
			get {
				return demux.VideoPosition;
			}
			set {
				demux.VideoPosition = value;
			}
		}

		public override float lastFrameDecodeTime { get { return _lastFrameDecodeTime; } }
		public override int lastFrameSizeBytes { get { return _lastFrameSizeBytes; } }

		public override float totalDecodeTime { get { return _totalDecodeTime; } }
		public override long totalSizeBytes { get { return _totalSizeBytes; } }

		#endregion

		#region ----- Private members -----

		protected Texture2D framebuffer;
		protected VideoStreamInfo streamInfo;
		protected Demux demux;
		protected float _lastFrameDecodeTime;
		protected int _lastFrameSizeBytes;
		protected float _totalDecodeTime;
		protected long _totalSizeBytes;
		protected System.Diagnostics.Stopwatch watch;

		#endregion
	}

}
