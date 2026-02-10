using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Telhai.DotNet.PlayerProject.Models;
using Telhai.DotNet.PlayerProject.Services;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace Telhai.DotNet.PlayerProject
{
    /// <summary>
    /// Interaction logic for MusicPlayer.xaml
    /// </summary>
    public partial class MusicPlayer : Window
    {
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer timer = new DispatcherTimer();
        private DispatcherTimer imagesTimer = new DispatcherTimer();
        private List<MusicTrack> library = new List<MusicTrack>();
        private bool isDragging = false;
        private const string FILE_NAME = "library.json";

        // Services
        private readonly ItunesService _itunes = new ItunesService();
        private readonly MetadataCacheService _cache = new MetadataCacheService();
        private CancellationTokenSource? _apiCts;
        private SongMetadata? _currentMetadata;
        private int _imageIndex = 0;

        public MusicPlayer()
        {
            //--init all Hardcoded xaml into Elements Tree
            InitializeComponent();

            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += new EventHandler(Timer_Tick);

            imagesTimer.Interval = TimeSpan.FromSeconds(4);
            imagesTimer.Tick += ImagesTimer_Tick;

            this.Loaded += MusicPlayer_Loaded;
            // this.MouseDoubleClick += MusicPlayer_MouseDoubleClick;
            // this.MouseDoubleClick += new MouseButtonEventHandler(MusicPlayer_MouseDoubleClick);
        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            this.LoadLibrary();
        }


        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Update slider ONLY if music is loaded AND user is NOT holding the handle
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan && !isDragging)
            {
                sliderProgress.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                sliderProgress.Value = mediaPlayer.Position.TotalSeconds;
            }
        }

        private void ImagesTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentMetadata == null) return;
            // If local images exist, cycle them. Otherwise do nothing (API artwork handled separately)
            if (_currentMetadata.LocalImages != null && _currentMetadata.LocalImages.Count > 0)
            {
                _imageIndex = (_imageIndex + 1) % _currentMetadata.LocalImages.Count;
                var path = _currentMetadata.LocalImages[_imageIndex];
                if (File.Exists(path))
                {
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.UriSource = new System.Uri(path);
                        bi.EndInit();
                        imgCover.Source = bi;
                    }
                    catch { }
                }
            }
        }

        // --- EMPTY PLACEHOLDERS TO MAKE IT BUILD ---
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            //if (sender is Button btn)
            //{
            //    btn.Background = Brushes.LightGreen;
            //}
            

            // If an item is selected, play that first and trigger metadata load
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                _ = StartPlaybackAndLoadMetadataAsync(track);
                return;
            }

            mediaPlayer.Play();
            timer.Start();
            txtStatus.Text = "Playing";
        }
        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
            txtStatus.Text = "Paused";
        }
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            timer.Stop();
            sliderProgress.Value = 0;
            txtStatus.Text = "Stopped";
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaPlayer.Volume = sliderVolume.Value;
        }

        private void Slider_DragStarted(object sender, MouseButtonEventArgs e)
        {
            isDragging = true; // Stop timer updates
        }

        private void Slider_DragCompleted(object sender, MouseButtonEventArgs e)
        {
            isDragging = false; // Resume timer updates
            mediaPlayer.Position = TimeSpan.FromSeconds(sliderProgress.Value);
        }


        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            //File Dialog to choose file from system
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "MP3 Files|*.mp3";

            //User Confirmed
            if (ofd.ShowDialog() == true)
            {
                //iterate all files selected as tring
                foreach (string file in ofd.FileNames)
                {
                    //Create Object for each filr
                    MusicTrack track = new MusicTrack
                    {
                        //Only file name
                        Title = System.IO.Path.GetFileNameWithoutExtension(file),
                        //full path
                        FilePath = file
                    };
                    library.Add(track);
                }
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

        private void UpdateLibraryUI()
        {
            //Take All library list as Source to the listbox
            //diaplay tostring for inner object whithin list
            lstLibrary.ItemsSource = null;
            lstLibrary.ItemsSource = library;
        }

        private void SaveLibrary()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(library, options);
            File.WriteAllText(FILE_NAME, json);
        }

        private void LoadLibrary()
        {
            if (File.Exists(FILE_NAME))
            {
                //read File
                string json = File.ReadAllText(FILE_NAME);
                //Create List Of MusicTrack from json
                library = JsonSerializer.Deserialize<List<MusicTrack>>(json) ?? new List<MusicTrack>();
                //Show All loaded MusicTrack in List Box
                UpdateLibraryUI();
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                library.Remove(track);
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

        private void LstLibrary_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                _ = StartPlaybackAndLoadMetadataAsync(track);
            }
        }

        private void lstLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                // show basic info and file path immediately
                txtCurrentSong.Text = track.Title;
                txtFilePath.Text = track.FilePath;

                // load cached metadata if present (no API call)
                _ = LoadCachedMetadataAsync(track);
            }
        }

        private async Task LoadCachedMetadataAsync(MusicTrack track)
        {
            var cached = await _cache.LoadAsync(track.FilePath);
            if (cached != null)
            {
                _currentMetadata = cached;
                UpdateMetadataUI(cached);
                if (cached.LocalImages != null && cached.LocalImages.Count > 0)
                {
                    _imageIndex = 0;
                    try
                    {
                        var first = cached.LocalImages[0];
                        if (File.Exists(first))
                        {
                            var bi = new BitmapImage();
                            bi.BeginInit();
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.UriSource = new System.Uri(first);
                            bi.EndInit();
                            imgCover.Source = bi;
                            imagesTimer.Start();
                        }
                    }
                    catch { }
                }
            }
            else
            {
                // show filename without extension as fallback
                txtMetaTitle.Text = System.IO.Path.GetFileNameWithoutExtension(track.FilePath);
                txtMetaArtist.Text = "";
                txtMetaAlbum.Text = "";
                imgCover.Source = (DrawingImage)Application.Current.Resources["DefaultCover"];
            }
        }

        private void UpdateMetadataUI(SongMetadata meta)
        {
            Dispatcher.Invoke(() =>
            {
                txtMetaTitle.Text = string.IsNullOrWhiteSpace(meta.TrackName) ? System.IO.Path.GetFileNameWithoutExtension(meta.FilePath) : meta.TrackName;
                txtMetaArtist.Text = meta.ArtistName;
                txtMetaAlbum.Text = meta.AlbumName;
                txtFilePath.Text = meta.FilePath;

                // If no local images, use artwork from API if available
                if ((meta.LocalImages == null || meta.LocalImages.Count == 0) && !string.IsNullOrWhiteSpace(meta.ArtworkUrl))
                {
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new System.Uri(meta.ArtworkUrl);
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        imgCover.Source = bi;
                    }
                    catch
                    {
                        imgCover.Source = (DrawingImage)Application.Current.Resources["DefaultCover"];
                    }
                }
            });
        }

        private async Task StartPlaybackAndLoadMetadataAsync(MusicTrack track)
        {
            // Open and play
            try
            {
                mediaPlayer.Open(new Uri(track.FilePath));
                mediaPlayer.Play();
                timer.Start();
                txtCurrentSong.Text = track.Title;
                txtStatus.Text = "Playing";
            }
            catch { }

            // Cancel previous API call
            _apiCts?.Cancel();
            _apiCts = new CancellationTokenSource();
            var ct = _apiCts.Token;

            // Try load cache first
            var cached = await _cache.LoadAsync(track.FilePath);
            if (cached != null)
            {
                _currentMetadata = cached;
                UpdateMetadataUI(cached);
                if (cached.LocalImages != null && cached.LocalImages.Count > 0)
                {
                    imagesTimer.Start();
                }
                return;
            }

            // Build search query from filename (remove extension) - may contain artist - title
            string filename = System.IO.Path.GetFileNameWithoutExtension(track.FilePath);
            string query = filename.Replace('-', ' ');

            SongMetadata? meta = null;
            try
            {
                meta = await _itunes.SearchAsync(query, ct);
                if (meta != null)
                {
                    meta.FilePath = track.FilePath;
                    await _cache.SaveAsync(track.FilePath, meta);
                    _currentMetadata = meta;
                    UpdateMetadataUI(meta);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch
            {
                // on error show filename and path
                Dispatcher.Invoke(() =>
                {
                    txtMetaTitle.Text = System.IO.Path.GetFileNameWithoutExtension(track.FilePath);
                    txtMetaArtist.Text = "";
                    txtMetaAlbum.Text = "";
                    txtFilePath.Text = track.FilePath;
                });
            }
        }

        private void BtnEditSong_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                var w = new EditSongWindow(track.FilePath);
                w.Owner = this;
                w.ShowDialog();
                // reload cache after edit
                _ = LoadCachedMetadataAsync(track);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            //1) Create Settings Window Instance
            Settings settingsWin = new Settings();

            //2) Subscribe/register to OnScanCompleted Event
            settingsWin.OnScanCompleted += SettingsWin_OnScanCompleted;

            settingsWin.ShowDialog();

        }

        private void SettingsWin_OnScanCompleted(List<MusicTrack> newTracksEventData)
        {
            foreach (var track in newTracksEventData)
            {
                // Prevent duplicates based on FilePath
                if (!library.Any(x => x.FilePath == track.FilePath))
                {
                    library.Add(track);
                }
            }

            UpdateLibraryUI();
            SaveLibrary();
        }
    }



    //private void MusicPlayer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    //{
    //    MainWindow p = new MainWindow();
    //    p.Title = "YYYYY";
    //    p.Show();
    //}
}