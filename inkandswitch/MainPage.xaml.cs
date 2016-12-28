using System;
using System.Collections.Generic;
using Windows.System;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Linq;
using System.Collections;
using Windows.UI;

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

            var attributes = LeftCanvas.InkPresenter.CopyDefaultDrawingAttributes();
            attributes.Color = Colors.Blue;
            attributes.Size = new Windows.Foundation.Size(10, 5);
            LeftCanvas.InkPresenter.UpdateDefaultDrawingAttributes(attributes);

            attributes = RightCanvas.InkPresenter.CopyDefaultDrawingAttributes();
            attributes.Color = Colors.Red;
            RightCanvas.InkPresenter.UpdateDefaultDrawingAttributes(attributes);

            leftCanvasContext = new CanvasContext(LeftCanvas);
            leftCanvasContext.AddEventHandlers(RightCanvas);
            rightCanvasContext = new CanvasContext(RightCanvas);
            rightCanvasContext.AddEventHandlers(LeftCanvas);
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
                    leftCanvasContext.Undo();
                }
                else if (VirtualKey.Y == e.Key)
                {
                    leftCanvasContext.Redo();
                }
            }
        }

        private bool isCtrlPressed = false;


        class CanvasContext
        {
            public CanvasContext(InkCanvas canvas)
            {
                this.canvas = canvas;

                contextId = NextContextSequence();

                canvas.InkPresenter.StrokesCollected += PushStrokeCommand;
            }

            private void PushStrokeCommand(InkPresenter sender, InkStrokesCollectedEventArgs args)
            {
                undoStack.Push(new StrokeData(contextId, NextStrokeId(), args.Strokes));
            }

            public void AddEventHandlers(InkCanvas otherCanvas)
            {
                other = otherCanvas;

                other.InkPresenter.StrokesCollected += ApplyOtherStrokesToCanvas;

                other.InkPresenter.StrokeInput.StrokeStarted += OnOtherStrokeStarted;
                other.InkPresenter.StrokeInput.StrokeContinued += OnOtherStrokeContinued;
                other.InkPresenter.StrokeInput.StrokeEnded += OnOtherStrokeEnded;
                other.InkPresenter.StrokeInput.StrokeCanceled += OnOtherStrokeCanceled;
            }

            private void ApplyOtherStrokesToCanvas(InkPresenter sender, InkStrokesCollectedEventArgs args)
            {
                // Need to do a deep copy of the strokes, as one stroke can't exist in two separate canvases
                foreach (var stroke in args.Strokes)
                {
                    canvas.InkPresenter.StrokeContainer.AddStroke(stroke.Clone());
                }

                DiscardCurrentWetStroke();
            }

            private void OnOtherStrokeCanceled(InkStrokeInput sender, Windows.UI.Core.PointerEventArgs args)
            {
                DiscardCurrentWetStroke();
            }

            private void OnOtherStrokeEnded(InkStrokeInput sender, Windows.UI.Core.PointerEventArgs args)
            {
                ContinueStroke(args);
            }

            private void OnOtherStrokeContinued(InkStrokeInput sender, Windows.UI.Core.PointerEventArgs args)
            {
                ContinueStroke(args);
            }

            private void OnOtherStrokeStarted(InkStrokeInput sender, Windows.UI.Core.PointerEventArgs args)
            {
                inkPoints = new List<InkPoint>();
                ContinueStroke(args);
            }

            private void ContinueStroke(Windows.UI.Core.PointerEventArgs args)
            {
                InkPoint point = new InkPoint(args.CurrentPoint.Position, args.CurrentPoint.Properties.Pressure);

                inkPoints.Add(point);

                var attributes = other.InkPresenter.CopyDefaultDrawingAttributes();
                strokeBuilder.SetDefaultDrawingAttributes(attributes);
                var newStroke = strokeBuilder.CreateStrokeFromInkPoints(inkPoints, System.Numerics.Matrix3x2.Identity);

                ApplyStrokeToCanvas(canvas, newStroke);

                DiscardCurrentWetStroke();

                wetStroke = newStroke;
            }

            private void DiscardCurrentWetStroke()
            {
                if (null != wetStroke)
                {
                    wetStroke.Selected = true;
                    canvas.InkPresenter.StrokeContainer.DeleteSelected();
                    wetStroke = null;
                }
            }

            private void ApplyStrokeToCanvas(InkCanvas destinationCanvas, InkStroke stroke)
            {
                destinationCanvas.InkPresenter.StrokeContainer.AddStroke(stroke);
            }

            public void Undo()
            {
                System.Diagnostics.Debug.WriteLine("Undo!");

                if (undoStack.Count > 0)
                {
                    StrokeData data = undoStack.Pop();
                    redoStack.Push(data.Clone());
                    data.Select();
                    canvas.InkPresenter.StrokeContainer.DeleteSelected();
                }
            }

            public void Redo()
            {
                System.Diagnostics.Debug.WriteLine("Redo!");

                if (redoStack.Count > 0)
                {
                    StrokeData data = redoStack.Pop();
                    foreach (InkStroke stroke in data.Strokes)
                    {
                        ApplyStrokeToCanvas(canvas, stroke);
                    }
                    undoStack.Push(data);
                }
            }

            private int NextStrokeId()
            {
                return ++strokeSequence;
            }

            static private int NextContextSequence()
            {
                return ++s_contextSequence;
            }

            private InkCanvas canvas;
            private InkCanvas other;
            private List<InkPoint> inkPoints = null;
            private InkStroke wetStroke = null;
            private InkStrokeBuilder strokeBuilder = new InkStrokeBuilder();
            
            class StrokeData
            {
                public StrokeData(int contextId, int id, IReadOnlyList<InkStroke> strokes)
                {
                    this.contextId = contextId;
                    this.id = id;
                    this.strokes = strokes;
                }

                public StrokeData Clone()
                {
                    return new StrokeData(contextId, id, strokes.Select(i => i.Clone()).ToList());
                }

                public void Select()
                {
                    foreach (var stroke in strokes)
                    {
                        stroke.Selected = true;
                    }
                }

                public int ContextId { get { return contextId; } }
                public int Id { get { return id; } }
                public IReadOnlyList<InkStroke> Strokes { get { return strokes; } }

                private int contextId;
                private int id;
                private IReadOnlyList<InkStroke> strokes;
            }

            private Stack<StrokeData> undoStack = new Stack<StrokeData>();
            private Stack<StrokeData> redoStack = new Stack<StrokeData>();
            private int strokeSequence = 0;
            private int contextId;

            private static int s_contextSequence = 0;
        }

        private CanvasContext leftCanvasContext;
        private CanvasContext rightCanvasContext;
    }
}
