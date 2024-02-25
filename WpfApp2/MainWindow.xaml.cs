using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfApp2
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private bool isIncreasing = false;

        private Point lastMousePosition;

        private int maxGridLines = 24;
        private int midGridLines = 15; 

        private int xLen, xOffset, xCountLines;
        private int yLen, yOffset, yCountLines;
        private double xScaleGrid, xScalePlot;
        private double yScaleGrid, yScalePlot;

        private RectangleGeometry clipGeometry;
        private int zoomScale = 0;

        public List<ObservableCollection<Data>> Datas = new List<ObservableCollection<Data>>();
        public MainWindow()
        {
            InitializeComponent();

            ComboBox.SelectedIndex = 0;

            clipGeometry = new RectangleGeometry(new Rect(0, 0, canvas.Width, canvas.Height));

            var data = new ObservableCollection<Data>();

            data.Add(new Data { X = 0, Y = 0 });
            data.Add(new Data { X = 1, Y = 3 });
            data.Add(new Data { X = 3, Y = 5 });

            xOffset = -data.Min(point => point.X);
            yOffset = -data.Min(point => point.Y);
            Datas.Add(data);

            dataGrid.ItemsSource = data;
            Draw(data);

            ComboBox.ItemsSource = new ObservableCollection<string> { "New file" };
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            int ind = ComboBox.SelectedIndex;
            Datas[ind].Add(new Data { X = 0, Y = 0 });
            DrawGraph(Datas[ind]);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All files (*.*)|*.*";
            int ind = ComboBox.SelectedIndex;

            if (saveFileDialog.ShowDialog() == true)
            {
                using (StreamWriter sw = new StreamWriter(saveFileDialog.FileName))
                {
                    foreach (var data in Datas[ind])
                    {
                        sw.WriteLine($"{data.X} {data.Y}");
                    }
                }
            }
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                ObservableCollection<string> fileNames = (ObservableCollection<string>)ComboBox.ItemsSource;
                string fileName = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);

                if (!string.IsNullOrEmpty(fileName) && !fileNames.Contains(fileName))
                {
                    fileNames.Add(fileName);
                    ComboBox.ItemsSource = new ObservableCollection<string>(fileNames);
                    var newData = new ObservableCollection<Data>();

                    using (StreamReader reader = new StreamReader(openFileDialog.FileName))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            string[] parts = line.Split(' ');
                            if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                            {
                                newData.Add(new Data { X = x, Y = y });
                            }
                        }
                    }
                    Datas.Add(newData);
                    ComboBox.SelectedIndex = fileNames.Count - 1;
                    dataGrid.ItemsSource = newData;
                }
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int ind = ComboBox.SelectedIndex;
            dataGrid.ItemsSource = Datas[ind];
            Draw((ObservableCollection<Data>)dataGrid.ItemsSource);
        }

        private void dataGridView_CurrentCellChanged(object sender, EventArgs e)
        {
            DrawGraph((ObservableCollection<Data>)dataGrid.ItemsSource);
        }

        private void Draw(ObservableCollection<Data> data)
        {
            int xMin = data.Min(point => point.X);
            int xMax = data.Max(point => point.X);
            int yMin = data.Min(point => point.Y);
            int yMax = data.Max(point => point.Y);

            xOffset = -xMin;
            xLen = xMax + xOffset;

            yOffset = -yMin;
            yLen = yMax + yOffset; 

            DrawGraph(data);
        }
        private void DrawGraph(ObservableCollection<Data> data)
        {
            canvas.Children.Clear();

            DefineStructureOfGrid(xLen, xOffset, out xCountLines, out xLen, out xOffset);
            xScaleGrid = canvas.Width / xCountLines;
            xScalePlot = canvas.Width / xLen;

            for (int x = 0; x <= xCountLines; x++)
            {
                Line line = new Line
                {
                    X1 = x * xScaleGrid,
                    Y1 = 0,
                    X2 = x * xScaleGrid,
                    Y2 = canvas.Height,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Clip = clipGeometry
                };

                canvas.Children.Add(line);

                TextBlock textBlock = new TextBlock
                {
                    Text = (-xOffset + x * (xLen / xCountLines)).ToString(),
                    Margin = new Thickness(x * xScaleGrid - 10, canvas.Height + 5, 0, 0)
                };

                canvas.Children.Add(textBlock);
            }

            DefineStructureOfGrid(yLen, yOffset, out yCountLines, out yLen, out yOffset);
            yScaleGrid = canvas.Height / yCountLines;
            yScalePlot = canvas.Height / yLen;

            for (int y = 0; y <= yCountLines; y++)
            {
                Line line = new Line
                {
                    X1 = 0,
                    Y1 = canvas.Height - y * yScaleGrid,
                    X2 = canvas.Width,
                    Y2 = canvas.Height - y * yScaleGrid,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Clip = clipGeometry
                };

                canvas.Children.Add(line);

                TextBlock textBlock = new TextBlock
                {
                    Text = (-yOffset + y * (yLen / yCountLines)).ToString(),
                    Margin = new Thickness(-30, canvas.Height - y * yScaleGrid - 10, 0, 0)
                };

                canvas.Children.Add(textBlock);
            }

            for (int i = 1; i < data.Count; i++)
            {
                DrawLineOfPlot(data[i - 1], data[i], xScalePlot, yScalePlot, xOffset, yOffset);
            }
        }

        private void DefineStructureOfGrid(int len, int offset, out int countLines, out int newLen, out int newOffset)
        {
            countLines = 0;
            newLen = len;
            newOffset = offset;
            while (countLines == 0)
            {
                if (len > midGridLines)
                {
                    for (int i = maxGridLines; i > midGridLines; i--)
                    {
                        if (newLen % i == 0)
                        {
                            countLines = i;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = midGridLines; i > 0; i--)
                    {
                        if (newLen % i == 0)
                        {
                            countLines = i;
                            break;
                        }
                    }
                }
                if (countLines == 0)
                {
                    if (isIncreasing)
                    {
                        newLen -= 2;
                        newOffset--;
                    }
                    else
                    {
                        newLen += 2;
                        newOffset++;
                    }
                }
            }
        }
        private void DrawLineOfPlot(Data startPoint, Data endPoint, double scaleX, double scaleY, double offsetX, double offsetY)
        {
            Line line = new Line
            {
                X1 = (startPoint.X + offsetX) * scaleX,
                Y1 = canvas.Height - (startPoint.Y + offsetY) * scaleY,
                X2 = (endPoint.X + offsetX) * scaleX,
                Y2 = canvas.Height - (endPoint.Y + offsetY) * scaleY,
                Stroke = Brushes.Blue,
                StrokeThickness = 1,
                Clip = clipGeometry
            };
            canvas.Children.Add(line);
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var thisData = (ObservableCollection<Data>)dataGrid.ItemsSource;
            var xMax = thisData.Max(point => point.X);
            var yMax = thisData.Max(point => point.Y);
            var isSmall = false;

            if (sender == labelX) // масштабирование по оси X
            {
                if (e.Delta > 0)
                {
                    if (xLen > 2)
                    {
                        zoomScale = -1;
                        isIncreasing = true;
                    }
                    else isSmall = true;
                }
                else if (e.Delta < 0)
                {
                    zoomScale = 1;
                    isIncreasing = false;
                }

                if (!isSmall)
                {
                    xOffset += zoomScale;
                    xLen += 2 * zoomScale;
                    DrawGraph((ObservableCollection<Data>)dataGrid.ItemsSource);
                }
            }
            else if (sender == labelY) // масштабирование по оси Y
            {
                if (e.Delta > 0)
                {
                    if (yLen > 2)
                    {
                        zoomScale = -1;
                        isIncreasing = true;
                    }
                    else isSmall = true;
                }
                else if (e.Delta < 0)
                {
                    zoomScale = 1;
                    isIncreasing = false;
                }

                if (!isSmall)
                {
                    yOffset += zoomScale;
                    yLen += 2 * zoomScale;
                    DrawGraph((ObservableCollection<Data>)dataGrid.ItemsSource);
                }
            }
            else if (sender == canvas) // общее масштабирование
            {
                if (e.Delta > 0)
                {
                    zoomScale = -1;
                    isIncreasing = true;

                    if (xLen > 2)
                    {
                        xOffset += zoomScale;
                        xLen += 2 * zoomScale;
                    }
                    if (yLen > 2)
                    {
                        yOffset += zoomScale;
                        yLen += 2 * zoomScale;
                    }
                    DrawGraph((ObservableCollection<Data>)dataGrid.ItemsSource);
                }
                else if (e.Delta < 0)
                {
                    zoomScale = 1;
                    isIncreasing = false;

                    xOffset += zoomScale;
                    xLen += 2 * zoomScale;

                    yOffset += zoomScale;
                    yLen += 2 * zoomScale;

                    DrawGraph((ObservableCollection<Data>)dataGrid.ItemsSource);
                }
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isDragging = true;
                lastMousePosition = e.GetPosition(canvas);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentMousePosition = e.GetPosition(canvas);

                double deltaX = (currentMousePosition.X - lastMousePosition.X) * 0.1;
                double deltaY = (currentMousePosition.Y - lastMousePosition.Y) * 0.1;

                xOffset += (int)Math.Round(deltaX);
                yOffset -= (int)Math.Round(deltaY);

                lastMousePosition = currentMousePosition;

                DrawGraph((ObservableCollection<Data>)dataGrid.ItemsSource);
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isDragging = false;
            }
        }

    }
}
public class Data
{
    public int X { get; set; }
    public int Y { get; set; }
}
