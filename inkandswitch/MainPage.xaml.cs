using System;
using System.Collections.Generic;
using Windows.System;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Linq;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace inkandswitch
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            this.Loaded += OnLoaded;

            ConfigureCanvasInput(this.LeftCanvas);
            ConfigureCanvasInput(this.RightCanvas);

            LeftCanvas.InkPresenter.StrokesCollected += (sender, args) => ApplyStrokesToCanvas(RightCanvas, args);
            RightCanvas.InkPresenter.StrokesCollected += (sender, args) => ApplyStrokesToCanvas(LeftCanvas, args);
        }

        private void ApplyStrokesToCanvas(InkCanvas destinationCanvas, InkStrokesCollectedEventArgs args)
        {
            // Need to do a deep copy of the strokes, as one stroke can't exist in two separate canvases
            foreach (var stroke in args.Strokes)
            {
                destinationCanvas.InkPresenter.StrokeContainer.AddStroke(stroke.Clone());
            }
        }

        private void ConfigureCanvasInput(InkCanvas canvas)
        {
            canvas.InkPresenter.InputDeviceTypes =
                Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                Windows.UI.Core.CoreInputDeviceTypes.Pen |
                Windows.UI.Core.CoreInputDeviceTypes.Touch;
        }
        
        private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Give input focus to the entire page
            this.Focus(Windows.UI.Xaml.FocusState.Programmatic);
        }

        private void OnKeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OnKeyUp");
            if (VirtualKey.Control == e.Key)
            {
                isCtrlPressed = false;
            }
        }

        private void OnKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OnKeyDown");
            if (VirtualKey.Control == e.Key)
            {
                isCtrlPressed = true;
            }
            else if (isCtrlPressed)
            {
                if (VirtualKey.Z == e.Key)
                {
                    Undo(LeftCanvas);
                }
            }
        }

        private void Undo(InkCanvas canvas)
        {
            System.Diagnostics.Debug.WriteLine("Undo!");

            IReadOnlyList<InkStroke> strokes = canvas.InkPresenter.StrokeContainer.GetStrokes();
            if (strokes.Count > 0)
            {
                strokes.Last().Selected = true;
                canvas.InkPresenter.StrokeContainer.DeleteSelected();
            }
        }

        private bool isCtrlPressed = false;
    }
}
