﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

//	http://bigflake.com/mediacodec/ExtractMpegFramesTest_egl14.java.txt

class TVideoDecoder
{
	public AndroidJavaObject	Surface;
	public AndroidJavaObject	SurfaceTexture;
	public AndroidJavaObject	Decoder;
	public Texture2D			Texture;
};

class TMediaFormat
{
	public int		ColourFormat = -1;
	public int		Width = 0;
	public int		Height = 0;
	public String	Mime = "";
	public String 	Description = "";
	public AndroidJavaObject	Format = null;

	public TMediaFormat()
	{
	}

	public TMediaFormat(AndroidJavaObject MediaFormat)
	{
		Format = MediaFormat;
	}
};


public class MediaCodec : MonoBehaviour {

	public string mFilename = "stadium.ogg";
	private static List<string> mLog = new List<string>();

	[NonSerialized]
	private TVideoDecoder mDecoder;
	[NonSerialized]
	private AndroidJavaObject mExtractor;
	private bool mFinishedExtracting = false;
	private bool mFinishedDecoding = false;
	private bool mInitialised = false;

	const long TIMEOUT_USEC = 100;
	const int INFO_TRY_AGAIN_LATER = -1;
	const int INFO_OUTPUT_BUFFERS_CHANGED = -3;
	const int INFO_OUTPUT_FORMAT_CHANGED = -2;
	const int OTHER_ERROR = -99;
	const int BUFFER_FLAG_END_OF_STREAM = 0x00000004;
	const int GL_TEXTURE_EXTERNAL_OES = 0x00008d65;

	public static void Log(String Message)
	{
		mLog.Add (Message);
		while (mLog.Count > 30)
			mLog.RemoveAt (0);
	}

	private static AndroidJavaObject LoadExtractor(String AssetFilename)
	{
		try
		{
			//	copy file
			#if UNITY_EDITOR
			String StreamingFilename = "file:" + System.IO.Path.Combine(Application.streamingAssetsPath, AssetFilename);
			#else
			String StreamingFilename = System.IO.Path.Combine(Application.streamingAssetsPath, AssetFilename);
			#endif
			String PersistFilename = System.IO.Path.Combine(Application.persistentDataPath, AssetFilename);
			Log("Loading www(" + StreamingFilename + ")");
			WWW StreamingFile = new WWW(StreamingFilename);
			while ( !StreamingFile.isDone )
			{
			//	Log("loading...." + StreamingFile.bytesDownloaded );
			}
			if ( StreamingFile.error != null )
			{
				Log ("www error: " + StreamingFile.error );
				return null;
			}

		//	Log("Writing to persistent: " + PersistFilename );
			System.IO.File.WriteAllBytes ( PersistFilename, StreamingFile.bytes );

			AndroidJavaObject Extractor = new AndroidJavaObject("android.media.MediaExtractor");
			String LoadFilename = PersistFilename;
			
			if (!System.IO.File.Exists(LoadFilename))
			{
				Log ("File does not exist: " + LoadFilename );
				return null;
			}

			var FileInfo = new System.IO.FileInfo(LoadFilename);
			//Log("file exists: " + FileInfo.Length);
			
			/*
			//	make ram file
			mLog += "making ram file \n";
			AndroidJavaObject RamFile = new AndroidJavaObject("android.io.RandomAccessFile", "myfile", "wb" );
			RamFile.Call("write", StreamingFile.bytes );
			AndroidJavaObject RamFileDescriptor = RamFile.Call<AndroidJavaObject>("getFD");
			mLog += "setDataSource RAMFILE" + Filename + "\n";
			extractor.Call("setDataSource",RamFileDescriptor);
			extractor.Call("setDataSource",Filename);
*/
			
			Log ("setDataSource " + LoadFilename );
			Extractor.Call("setDataSource",LoadFilename);

			int TrackCount = Extractor.Call<int>("getTrackCount");
			if ( TrackCount < 1 )
			{
				Log("tracks: " + TrackCount);
				return null;
			}
			return Extractor;
		}
		catch ( Exception e )
		{
			Log ("Exception: " + e.Message );
			return null;
		}
	}

	int GetTrack(AndroidJavaObject Extractor,String Filter="video/")
	{
		return 0;
	}


	TMediaFormat GetFormat(AndroidJavaObject Extractor)
	{
		int TrackCount = Extractor.Call<int>("getTrackCount");
		//Log ("tracks: " + TrackCount);
		int TrackIndex = GetTrack (Extractor);
		//Log ("selectTrack " + TrackIndex);
		Extractor.Call ("selectTrack",TrackIndex);
		
	//	Log ("getTrackFormat " + TrackIndex);
		try
		{
			TMediaFormat MediaFormat = new TMediaFormat();
			MediaFormat.Format = Extractor.Call<AndroidJavaObject>("getTrackFormat",TrackIndex);
			String FormatDescription = MediaFormat.Format.Call<String>("toString");
		//	Log("format.toString() = " + FormatDescription );
			
			//	sdk literals
			const String KEY_MIME = "mime";
			const String KEY_WIDTH = "width";
			const String KEY_HEIGHT = "height";

			MediaFormat.Mime = MediaFormat.Format.Call<string>("getString", KEY_MIME );
			MediaFormat.Width = MediaFormat.Format.Call<int>("getInteger", KEY_WIDTH );
			MediaFormat.Height = MediaFormat.Format.Call<int>("getInteger", KEY_HEIGHT );
			return MediaFormat;
		}
		catch ( Exception e )
		{
			Log ("GetFormatException: " + e.Message );
			return null;
		}
	}

	static Texture2D CreateDecoderTexture(int Width,int Height)
	{
		//	create a texture via java's low level gl access
		AndroidJavaClass GLES20 = new AndroidJavaClass ("android.opengl.GLES20");
		int[] Textures = new int[1];
		GLES20.CallStatic ("glGenTextures", 1, Textures, 0 );
		if (!glGetError ("glGenTextures"))
			return null;
		if (Textures [0] == 0) {
			Log ("Failed to generate texture");
			return null;
		}
		Texture2D UnityTexture = new Texture2D (Width,Height);
		IntPtr TextureId = (IntPtr)Textures [0];
		UnityTexture.UpdateExternalTexture ( TextureId );
		return UnityTexture;
		/*
		GLES20.CallStatic                  glBindTexture(GL_TEXTURE_EXTERNAL_OES, textureId);
		                  glTexParameterf(GL_TEXTURE_EXTERNAL_OES, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
		                  glTexParameterf(GL_TEXTURE_EXTERNAL_OES, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
		                  glTexParameterf(GL_TEXTURE_EXTERNAL_OES, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
		                  glTexParameterf(GL_TEXTURE_EXTERNAL_OES, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
		                  glBindTexture(GL_TEXTURE_EXTERNAL_OES, 0);
		                  
*/
	}

	static TVideoDecoder CreateDecoder(AndroidJavaObject Extractor,TMediaFormat Format)
	{
		TVideoDecoder Decoder = new TVideoDecoder ();
		Decoder.Texture = CreateDecoderTexture (Format.Width,Format.Height);
		if (Decoder.Texture == null)
			return null;

		try
		{
			int TextureId = Decoder.Texture.GetNativeTextureID();
			Decoder.SurfaceTexture = new AndroidJavaObject("android.graphics.SurfaceTexture", TextureId );
			Decoder.Surface = new AndroidJavaObject("android.view.Surface", Decoder.SurfaceTexture );
		}
		catch ( Exception e )
		{
			Log ("CreateDecoder::surface:: " + e.Message );
			return null;
		}

		try
		{
		//	Log ("Creating codec for " + Format.Mime );
			AndroidJavaClass MediaCodecClass = new AndroidJavaClass("android.media.MediaCodec");
			Decoder.Decoder = MediaCodecClass.CallStatic<AndroidJavaObject>("createDecoderByType", Format.Mime );

			//	"configure not found" when supplying surface....
			Decoder.Decoder.Call("configure", Format.Format, Decoder.Surface, null, 0);
			Decoder.Decoder.Call("start");
		}
		catch ( Exception e )
		{
			Log ("CreateDecoder::create decoder:: " + e.Message );
			return null;
		}

		return Decoder;
	}

	int GetAndroidSDKLevel()
	{
		try
		{
			AndroidJavaClass VersionClass = new AndroidJavaClass ("android.os.Build$VERSION");
			string Version = VersionClass.GetStatic<string> ("RELEASE");
			int VersionInt = VersionClass.GetStatic<int> ("SDK_INT");
			return VersionInt;
		}
		catch ( Exception e )
		{
			Log ("GetAndroidSDKLevel:: " + e.Message );
			return -1;
		}
	}


	void Init () {

		int SdkLevel = GetAndroidSDKLevel ();
		Log ("Android SDK " + SdkLevel);

		mExtractor = LoadExtractor (mFilename);
		if ( mExtractor == null )
				return;

		TMediaFormat Format = GetFormat( mExtractor );
		if (Format==null)
			return;
		Log ("Format of track is " + Format.Mime + " " + Format.Width + "x" + Format.Height);
		//jo.Call<android.media.MediaCodec>("createDecoderByType");

		mDecoder = CreateDecoder (mExtractor, Format);
		mInitialised = true;
	
	}

	AndroidJavaObject GetDecoderInputBuffer(AndroidJavaObject Decoder,int BufferIndex)
	{
		/*
Truman:android-21 grahamr$ pwd
/Users/grahamr/Library/Android/sdk/platforms/android-21
Truman:android-21 grahamr$ javap -classpath android.jar -s android.media.MediaCodec
*/
		/*
		string signature = "(I)Ljava/nio/ByteBuffer;";

		IntPtr methodID = AndroidJNIHelper.GetMethodID( JavaObject.GetRawClass(), MethodName, signature, false );
		Log ("Method ID (" + signature + ")== " + methodID);
		return null;
		string signature = "(i)ByteBuffer";
		IntPtr methodID = AndroidJNIHelper.GetMethodID( JavaObject.m_jclass, MethodName, signature, isStatic );

		jvalue[] array = AndroidJNIHelper.CreateJNIArgArray (args);
		public static IntPtr GetMethodID (IntPtr javaClass, string methodName, string signature)
		{
			bool isStatic = false;
			return AndroidJNIHelper.GetMethodID (javaClass, methodName, signature, isStatic);
		}

		if (typeof(ReturnType) == typeof(AndroidJavaObject))
		{
			IntPtr jobject = AndroidJNISafe.CallObjectMethod (this.m_jobject, methodID, array);
			result = (ReturnType)((object)AndroidJavaObject.AndroidJavaObjectDeleteLocalRef (jobject));
		}
		else
			
			jvalue[] array = AndroidJNIHelper.CreateJNIArgArray (args);
		ReturnType result;
		*/

		//	gr: API 19 "getInputBuffer(X) seems to be missing? checked running API and it's 19... hmmm
		//	so use bufferS instead and access element
		//AndroidJavaObject InputByteBuffer = Decoder.Call<AndroidJavaObject> ("getInputBuffer", InputBufferIndex);
		AndroidJavaObject[] InputBuffers = Decoder.Call<AndroidJavaObject[]> ("getInputBuffers");
		int BufferCount = InputBuffers.Length;
		if (BufferIndex >= BufferCount)
			return null;
		return InputBuffers [BufferIndex];
	}
	AndroidJavaObject GetDecoderOutputBuffer(AndroidJavaObject Decoder,int BufferIndex)
	{
		//AndroidJavaObject InputByteBuffer = Decoder.Call<AndroidJavaObject> ("getInputBuffer", InputBufferIndex);
		AndroidJavaObject[] Buffers = Decoder.Call<AndroidJavaObject[]> ("getOutputBuffers");
		int BufferCount = Buffers.Length;
		if (BufferIndex >= BufferCount)
			return null;
		return Buffers [BufferIndex];
	}

		//	return false to stop (on error)
	bool UpdateCodecInput(AndroidJavaObject Decoder,AndroidJavaObject Extractor)
	{
		//	get input buffer to write into codec
		int InputBufferIndex = Decoder.Call<int> ("dequeueInputBuffer", TIMEOUT_USEC);
		if (InputBufferIndex < 0)
			return true;
		
		//Log ("Got input buffer buffer #" + InputBufferIndex);
		AndroidJavaObject InputByteBuffer = GetDecoderInputBuffer (Decoder, InputBufferIndex);
		if (InputByteBuffer == null) {
			Log ("Error getting input buffer " + InputBufferIndex);
			return false;
		}
		
		//	pull latest data from extractor
		int ChunkSize = Extractor.Call<int> ("readSampleData", InputByteBuffer, 0);
		//Log ("Extracted chunk size " + ChunkSize);
		if (ChunkSize < 0)
			ChunkSize = 0;
		long presentationTimeUs = Extractor.Call<long> ("getSampleTime");
		//Log ("decoder presentation time " + presentationTimeUs);
		
		//	fill
		//	send to decoder
		int Flags = ChunkSize==0 ? BUFFER_FLAG_END_OF_STREAM : 0;
		Decoder.Call("queueInputBuffer", InputBufferIndex, 0, ChunkSize, presentationTimeUs, Flags );

		//	gr: seems to fail when we're out of data
		bool AdvancedOk = Extractor.Call<bool>("advance");	//	gr: can we put this with read?
		if (!AdvancedOk) {
			Log ("Extractor.advance() returned false");
			return false;
		}
		return true;
	}

	bool ChangeFormat(AndroidJavaObject NewFormat)
	{
		TMediaFormat MediaFormat = new TMediaFormat (NewFormat);

		return true;
	}

	static bool glGetError(string LastFunction)
	{
		AndroidJavaClass GLES20 = new AndroidJavaClass ("android.opengl.GLES20");
		int Error = GLES20.CallStatic<int> ("glGetError");
		if (Error == 0)
				return true;

		Log ("GL error: " + Error + " after " + LastFunction );
		return false;
	}

	bool UpdateCodecOutput(AndroidJavaObject Decoder)
	{
		AndroidJavaObject BufferInfo = new AndroidJavaObject("android.media.MediaCodec$BufferInfo");
		
		//	returns status if not buffer index
		int OutputBufferIndex = Decoder.Call<int>("dequeueOutputBuffer", BufferInfo, TIMEOUT_USEC );
		
		//	got a format change, update texture etc
		if ( OutputBufferIndex == INFO_OUTPUT_FORMAT_CHANGED )
		{
			AndroidJavaObject MediaFormat = Decoder.Call<AndroidJavaObject>("getOutputFormat");
			if ( !ChangeFormat( MediaFormat ) )
				return false;
			
			return true;
		}
		
		bool Ready = true;
		switch ( OutputBufferIndex )
		{
		case INFO_TRY_AGAIN_LATER:
		case INFO_OUTPUT_BUFFERS_CHANGED:
		case INFO_OUTPUT_FORMAT_CHANGED:
		case OTHER_ERROR:
			Ready = false;
			break;
			
		default:
			if ( OutputBufferIndex < 0 )
				Ready = false;
			break;
		}
		
		if ( !Ready )
		{
			//Log ("Not ready to output yet... " + OutputBufferIndex);
			return true;
		}
		
		//Log ("got Output buffer #" + OutputBufferIndex);
		bool EndOfStream = (BufferInfo.Get<int>("flags") & BUFFER_FLAG_END_OF_STREAM) != 0;
		if ( EndOfStream )
		{
			Log ("end of output stream");
			return false;
		}
		
		//	grab buffer
		AndroidJavaObject Buffer = GetDecoderOutputBuffer (Decoder, OutputBufferIndex);
		if ( Buffer == null ) {
			Log ("failed to get output buffer #" + OutputBufferIndex);
			return false;
		}
		
		bool DoRender = ( BufferInfo.Get<int>("size") != 0 );
		//Log ("Got buffer: render? " + DoRender);

		//	gr: too big to copy to memory! over 1gb!?
		//	copy to texture
		int ByteLength = AndroidJNI.GetArrayLength (Buffer.GetRawObject ());
		Log ("Byte buffer length: " + ByteLength);
//		byte[] Bytes = AndroidJNI.FromByteArray (Buffer.GetRawObject ());
		//byte[] Bytes = Buffer.Call<byte[]> ("array");
//		Log ("Copy " + Bytes.Length + "bytes to texture!");

		// As soon as we call releaseOutputBuffer, the buffer will be forwarded
		// to SurfaceTexture to convert to a texture.  The API doesn't guarantee
		// that the texture will be available before the call returns, so we
		// need to wait for the onFrameAvailable callback to fire.
		Decoder.Call("releaseOutputBuffer", OutputBufferIndex, DoRender);
		glGetError("releaseOutputBuffer");
		mDecoder.SurfaceTexture.Call ("updateTexImage");
		glGetError ("updateTexImage");
		//	outputSurface.awaitNewImage();
		//	outputSurface.drawImage(true);
		
		return true;
	}

	//	have to init in render thread so we can do GL stuff
	void OnPostRender()
	{
		if (!mInitialised)
			Init ();
	}

	// Update is called once per frame
	void Update () {

			if ( !mInitialised )
				return;
		
		/*
		if ( !mFinishedExtracting )
		{
			try
			{
				mFinishedExtracting = !UpdateCodecInput ( mDecoder.Decoder, mExtractor );
			}
			catch ( Exception e )
			{
				Log ("UpdateCodecInput:: " + e.Message);
			}
		}

		if ( !mFinishedDecoding )
		{
			try
			{
				mFinishedDecoding = !UpdateCodecOutput ( mDecoder.Decoder );
			}
			catch ( Exception e )
			{
				Log ("UpdateCodecOutput:: " + e.Message);
			}
		}
		*/
	}
	
	void OnGUI()
	{
		GUI.skin.button.wordWrap = true;
		int Size = 60;
		#if UNITY_EDITOR
		Size = 20;
		#endif
		GUI.Label (new Rect (0, 0, Screen.width, Screen.height), "<size=" + Size + ">" + string.Join("\n",mLog.ToArray()) + "</size>");
		
		
		if (mDecoder!=null && mDecoder.Texture) {
			int Width = mDecoder.Texture.width;
			int Height =  mDecoder.Texture.height;
			GUI.DrawTexture( new Rect ( Screen.width - Width, 0, Width, Height ), mDecoder.Texture );
		}
		
	}
}
