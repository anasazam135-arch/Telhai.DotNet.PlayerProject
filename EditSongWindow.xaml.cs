using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Telhai.DotNet.PlayerProject.Models;
using Telhai.DotNet.PlayerProject.Services;
using System.Threading.Tasks;

namespace Telhai.DotNet.PlayerProject
{
    public partial class EditSongWindow : Window
    {
        private readonly string _filePath;
        private readonly MetadataCacheService _cache = new MetadataCacheService();
        private SongMetadata? _meta;

        public EditSongWindow(string filePath)
        {
            InitializeComponent();
            _filePath = filePath;
            Loaded += EditSongWindow_Loaded;
        }

        private async void EditSongWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _meta = await _cache.LoadAsync(_filePath) ?? new SongMetadata { FilePath = _filePath, TrackName = System.IO.Path.GetFileNameWithoutExtension(_filePath) };
            txtTrackName.Text = _meta.TrackName;
            txtArtist.Text = _meta.ArtistName;
            txtAlbum.Text = _meta.AlbumName;
            lstImages.ItemsSource = _meta.LocalImages.ToList();
            SetCoverImage();
        }

        private void SetCoverImage()
        {
            if (_meta == null) return;
            string first = _meta.LocalImages?.FirstOrDefault() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(first) && File.Exists(first))
            {
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(first);
                    bi.EndInit();
                    imgEditorCover.Source = bi;
                    return;
                }
                catch { }
            }
            // fallback to default
            imgEditorCover.Source = (DrawingImage)Application.Current.Resources["DefaultCover"];
        }

        private void BtnAddImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
            if (ofd.ShowDialog() == true)
            {
                if (_meta != null)
                {
                    _meta.LocalImages.Add(ofd.FileName);
                    lstImages.ItemsSource = _meta.LocalImages.ToList();
                    SetCoverImage();
                }
            }
        }

        private void BtnRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if (lstImages.SelectedItem is string path && _meta != null)
            {
                _meta.LocalImages.Remove(path);
                lstImages.ItemsSource = _meta.LocalImages.ToList();
                SetCoverImage();
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_meta == null) return;
            _meta.TrackName = txtTrackName.Text;
            _meta.ArtistName = txtArtist.Text;
            _meta.AlbumName = txtAlbum.Text;
            await _cache.SaveAsync(_filePath, _meta);
            this.DialogResult = true;
            this.Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
