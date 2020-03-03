using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.IO;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Linq;
using ImageRecognizer;
using System.Threading;
using System.Reactive.Linq;
using ImageReadCS;
using System.Collections.Concurrent;
using DataBase;



namespace Lab_2
{
    public class MainWindow : Window
    {
        private List<string> imgFiles = new List<string>();
        private string imgDir;

        private Carousel ImagesCar;
        private ComboBox ImgCombo;
        private ComboBox NumCombo;
        private ListBox ImagesListBox;
        private TextBlock ProccText;
        private TextBlock AmountText;
        private TextBlock DBAmountText;
        private Button ClassButton;
        private Button StopButton;
        private Button DBButton;
        private Recognizer myRec;
        private List<Tuple<string, int>> result = new List<Tuple<string, int>>();
        private bool isProcessing = false;
        private List<Avalonia.Controls.Image> Images = new List<Avalonia.Controls.Image>();
        private Subject<List<string>> sourcePath = new Subject<List<string>>();
        private Subject<List<Avalonia.Controls.Image>> sourceImg = new Subject<List<Avalonia.Controls.Image>>();
        private Subject<List<string>> sourceLB = new Subject<List<string>>();
        private Subject<List<int>> sourceNum = new Subject<List<int>>();

        public MainWindow()
        {
            InitializeComponent();

            var comboImgBinding = ImgCombo.Bind(ComboBox.ItemsProperty, sourcePath);
            var carousBinding = ImagesCar.Bind(Carousel.ItemsProperty, sourceImg);
            var comboClassBinding = NumCombo.Bind(ComboBox.ItemsProperty, sourceNum);
            var listBocBinding = ImagesListBox.Bind(ListBox.ItemsProperty, sourceLB);

            sourceNum.OnNext(new List<int> { 0, 1, 2, 3, 4, 5 });

            ImgCombo.SelectionChanged += (s, e) =>
            {
                sourceImg.OnNext(new List<Avalonia.Controls.Image> { Images[ImgCombo.SelectedIndex] });
            };

            NumCombo.SelectionChanged += (s, e) =>
            {
                UpdateResults();
            };

            ClassButton.Click += (s, e) =>
            {
                StopButton.IsEnabled = true;
                ProccText.IsVisible = true;
                NumCombo.IsEnabled = true;
                if (!isProcessing) BeginSession();
            };
            StopButton.Click += (s, e) =>
            {
                if (myRec != null) myRec.Stop();
                isProcessing = false;
                StopButton.IsEnabled = false;
                ProccText.IsVisible = false;
            };

            DBButton.Click += (s, e) =>
            {
                int count;
                using (var db = new ImagesContext())
                {
                    var imgs = db.Images.ToList();
                    foreach (var im in imgs)
                    {
                        db.Images.Remove(im);
                    }
                    db.SaveChanges();
                    count = db.Images.ToList().Count;
                }
                DBAmountText.Text = "Pictures in database amount: " + count;
            };

            this.FindControl<Button>("ChooseDir").Click += async (s, e) =>
            {
                var dialog = new OpenFolderDialog();
                var res = await dialog.ShowAsync(GetWindow());

                if (res != null)
                {
                    imgDir = res.ToString();
                    UpdateImages(imgDir);
                    ClassButton.IsEnabled = true;
                }
            };
        }

        private void UpdateResults()
        {
            if (NumCombo.SelectedItem != null)
            {
                var q = from res in result
                        where res.Item2 == (int)NumCombo.SelectedItem
                        select res.Item1;
                sourceLB.OnNext(q.ToList());
            }
            AmountText.Text = "Processed pictures amount: " + result.Count;
            int count;
            using (var db = new ImagesContext())
            {
                count = db.Images.ToList().Count;
            }
            DBAmountText.Text = "Pictures in database amount: " + count;
        }


        private void UpdateImages(string directory)
        {
            imgFiles.Clear();
            Images.Clear();
            foreach (var file in Directory.GetFiles(directory).Where(a => a.Split("/").Last()[0] != '.').ToArray())
            {
                Console.WriteLine(file);
                var tmp = new Avalonia.Controls.Image();
                tmp.Source = new Avalonia.Media.Imaging.Bitmap(file);
                Images.Add(tmp);
                imgFiles.Add(file);

            }
            sourcePath.OnNext(imgFiles);
        }

        Window GetWindow() => (Window)this.VisualRoot;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            ImagesCar = this.FindControl<Carousel>("ImagesCarousel");
            ImgCombo = this.FindControl<ComboBox>("ImagesCombobox");
            ClassButton = this.FindControl<Button>("ClassButton");
            StopButton = this.FindControl<Button>("StopButton");
            DBButton = this.FindControl<Button>("DBButton");
            NumCombo = this.FindControl<ComboBox>("ClassesCombobox");
            ImagesListBox = this.FindControl<ListBox>("ImagesListBox");
            ProccText = this.FindControl<TextBlock>("ComputTextBlock");
            AmountText = this.FindControl<TextBlock>("PictAmountTextBlock");
            AmountText.Text = "Processed pictures amount: 0";
            DBAmountText = this.FindControl<TextBlock>("DBAmountTextBlock");
            int count;
            using (var db = new ImagesContext())
            {
                count = db.Images.ToList().Count;
            }
            DBAmountText.Text = "Pictures in database amount: " + count;
            ClassButton.IsEnabled = false;
        }

        private async void BeginSession()
        {
            string modelPath = "/Users/dari/Documents/onnx/model.onnx";
            myRec = new Recognizer(modelPath);
            myRec.InitializePath(imgDir);

            isProcessing = true;

            Thread disp = new Thread(() =>
            {
                while (isProcessing)
                {
                    Tuple<string, int> tmp;
                    if (myRec.results.TryDequeue(out tmp))
                    {
                        result.Add(tmp);
                        Dispatcher.UIThread.InvokeAsync(new Action(() =>
                        {
                            UpdateResults();
                        })).Wait();
                    } else
                    {
                        Thread.Sleep(5);
                    }
                    
                }

            });


            Thread session = new Thread(() =>
           {
               myRec.DoSession();
               Thread.Sleep(5);
               isProcessing = false;
               Dispatcher.UIThread.InvokeAsync(new Action(() =>
               {
                   StopButton.IsEnabled = false;
                   ProccText.IsVisible = false;
               })).Wait();
           });

            disp.Start();
            session.Start();


        }
    }
}