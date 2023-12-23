using Microsoft.AspNetCore.Components;
using OpenCvSharp;
using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.BlazorJS.Toolbox;
using VideoCaptureBlazor.CvElement;
using Array = System.Array;
using Timer = System.Timers.Timer;
using VideoCapture = VideoCaptureBlazor.CvElement.VideoCapture;

namespace VideoCaptureBlazor.Pages;

public partial class LocalVideoCaptureDemo
{
    private class FaceFeature
    {
        public FaceFeature(Rect face, Rect[] eyes)
        {
            Face = face;
            Eyes = eyes;
        }

        public Rect Face { get; set; }
        public Rect[] Eyes { get; set; } = Array.Empty<Rect>();
    }

    [Inject]
    private MediaDevicesService MediaDevicesService { get; set; }

    [Inject]
    private HttpClient HttpClient { get; set; }

    private ElementReference _canvasSrcRef;
    private readonly Timer _timer = new();
    private VideoCapture? _videoCapture;
    private HTMLCanvasElement? _canvasSrcEl;
    private CanvasRenderingContext2D? _canvasSrcCtx;
    private MediaStream? _mediaStream;
    private Mat? _src;
    private CascadeClassifier? _faceCascade;

    private CascadeClassifier? _eyesCascade;

    // Video source
    // https://github.com/intel-iot-devkit/sample-videos
    private const string TestVideo = "test-videos/face-demographics-walking-and-pause.mp4";
    private bool _beenInit;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (!_beenInit)
        {
            _beenInit = true;
            _faceCascade = await LoadCascadeClassifier("haarcascades/haarcascade_frontalface_default.xml");
            _eyesCascade = await LoadCascadeClassifier("haarcascades/haarcascade_eye.xml");
            _canvasSrcEl = new HTMLCanvasElement(_canvasSrcRef);
            _canvasSrcCtx = _canvasSrcEl.Get2DContext();
            _videoCapture = new VideoCapture();
            _videoCapture.Video.CrossOrigin = "anonymous"; // allows videos from other domains using cors
            _timer.Elapsed += Timer_Elapsed;
            _timer.Interval = 1000d / 60d;
            _timer.Enabled = true;
        }
    }

    private async Task<CascadeClassifier> LoadCascadeClassifier(string url)
    {
        var text = await HttpClient.GetStringAsync(url);
        await System.IO.File.WriteAllTextAsync("tmp.xml", text);
        var cascadeClassifier = new CascadeClassifier("tmp.xml");
        System.IO.File.Delete("tmp.xml");
        return cascadeClassifier;
    }

    // https://github.com/opencv/opencv/tree/master/rgbaBytes/haarcascades
    // https://github.com/VahidN/OpenCVSharp-Samples/blob/master/OpenCVSharpSample15/Program.cs
    // https://www.tech-quantum.com/have-fun-with-webcam-and-opencv-in-csharp-part-2/
    private List<FaceFeature> FaceDetect(Mat image)
    {
        var features = new List<FaceFeature>();
        var faces = DetectFaces(image);
        foreach (var item in faces)
        {
            // Get face region
            using var faceRoi = image[item];
            // Detect eyes in the face region
            var eyes = DetectEyes(faceRoi);
            // Add to results
            features.Add(new FaceFeature(face: item, eyes: eyes));
        }

        return features;
    }

    private static void MarkFeatures(Mat image, List<FaceFeature> features)
    {
        foreach (var feature in features)
        {
            Cv2.Rectangle(image, feature.Face, new Scalar(0, 255, 0), thickness: 1);
            using var faceRegion = image[feature.Face];
            foreach (var eye in feature.Eyes)
            {
                Cv2.Rectangle(faceRegion, eye, new Scalar(255, 0, 0), thickness: 1);
            }
        }
    }

    private Rect[] DetectEyes(Mat image)
    {
        var faces = _eyesCascade == null ? Array.Empty<Rect>() : _eyesCascade.DetectMultiScale(image, 1.3, 5);
        return faces;
    }

    private Rect[] DetectFaces(Mat image)
    {
        var faces = _faceCascade == null ? Array.Empty<Rect>() : _faceCascade.DetectMultiScale(image, 1.3, 5);
        return faces;
    }

    private void StopPlaying()
    {
        if (_videoCapture == null) return;
        _videoCapture.Video.Src = null;
        _videoCapture.Video.SrcObject = null;

        if (_mediaStream == null) return;
        _mediaStream.Dispose();
        _mediaStream = null;
    }

    private async Task PlayRemoteVideo()
    {
        if (_videoCapture == null) return;
        try
        {
            StopPlaying();
            _videoCapture.Video.Src = TestVideo;
        }
        catch
        {
            // ignored
        }
    }

    private async Task PlayUserMedia()
    {
        if (_videoCapture == null) return;
        try
        {
            StopPlaying();
            await MediaDevicesService.UpdateDeviceList(true);
            _mediaStream = await MediaDevicesService.MediaDevices.GetUserMedia();
            _videoCapture.Video.SrcObject = _mediaStream;
        }
        catch
        {
            // ignored
        }
    }

    private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_videoCapture == null) return;
        _src ??= new Mat();
        var succ = _videoCapture.Read(_src);
        if (!succ) return;
        var res = FaceDetect(_src);
        MarkFeatures(_src, res);
        _src.DrawOnCanvas(_canvasSrcCtx, true);
    }

    public void Dispose()
    {
        if (!_beenInit) return;

        _beenInit = false;
        _timer.Dispose();
        StopPlaying();
        _videoCapture?.Dispose();
        _videoCapture = null;
        _faceCascade?.Dispose();
        _eyesCascade?.Dispose();
        _canvasSrcEl?.Dispose();
        _canvasSrcCtx?.Dispose();
        _src?.Dispose();
    }
}