using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebScraper
{
    public partial class WebScraper : Form
    {
        readonly HttpClient client = new HttpClient();
        Dictionary<Task<byte[]>, string> downloads = new Dictionary<Task<byte[]>, string>();
        List<string> URLCollection = new List<string>();
        private string WebsiteURL { get; set; }
        public WebScraper()
        {
            InitializeComponent();
        }

        private async void buttonExtract_Click_1(object sender, EventArgs e)
        {
            string input = textBoxInput.Text;
            await ExtractHTMLAsync(input);
        }

        private async void buttonSave_Click(object sender, EventArgs e)
        {
            await SavePicturesAsync(ReturnSavePath());
        }

        private string ImgExtension(string imgUrl)
        {
            Regex imgExtension = new Regex(@"\.jpg|\.jpeg|\.png|\.gif|\.bmp");
            return imgExtension.Match(imgUrl).Value;
        }
        
        private string ReturnSavePath()
        {
            FolderBrowserDialog folder = new FolderBrowserDialog();
            folder.ShowDialog();

            return folder.SelectedPath;
        }

        public string GetFullPath(string selectedSavePath, string filename)
        {
            return Path.Combine(selectedSavePath, filename);
        }

        private void ResetApplication()
        {
            URLCollection.Clear();
            textBoxDisplayURLs.Text = "";
            buttonSave.Enabled = false;
            labelStatus.Text = $"Found {0} images";
        }

        private void CheckIfInputContainsHttp(string input)
        {
            if (!input.Contains("http://"))
                WebsiteURL = $"http://{input}";
            else
                WebsiteURL = input;
        }

        private string ImgName(int counter, string imgExtension)
        {
            string imageName = "Image" + (counter + 1) + imgExtension;
            return imageName;
        }

        private void AddURLCollectionToDownloads()
        {
            for (int i = 0; i < URLCollection.Count; i++)
            {
                downloads.Add(client.GetByteArrayAsync($"{URLCollection[i]}"), URLCollection[i]);
            }
        }

        private void AddRegexMatchesToURLCollection(MatchCollection matches)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                if (!matches[i].ToString().Contains("http"))
                {
                    URLCollection.Add($"{WebsiteURL}{matches[i]}");
                }
                else
                {
                    URLCollection.Add($"{matches[i]}");
                }
            }
        }

        private async Task<string> DownloadUrlAsync()
        {
            try
            {
                Task<string> download = client.GetStringAsync(WebsiteURL);
                await download;

                return download.Result;
            }
            catch
            {
                MessageBox.Show("The URL you requested could not be found.", "404 Error");
                return null;
            }
        }

        private async Task<List<string>> AddImgURLToListAsync(string downloadedUrl, CancellationToken cancellationToken)
        {
            Regex findImgTags = new Regex("(?<=<img[^h].+src=\")[h/].+?(.jpe?g|.png|.gif|.bmp)(?<=\"?)[^\"]*?(?=\".+>)");

            Task addToList = Task.Run(() =>
            {
                var matches = findImgTags.Matches(downloadedUrl);
                AddRegexMatchesToURLCollection(matches);
            });
            await addToList;

            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException(addToList);
            }

            return URLCollection;
        }

        private async Task<bool> AddImgURLToListWithTimeoutAsync(TimeSpan timeSpan, string downloadedUrl)
        {
            using (var cancellationToken = new CancellationTokenSource(timeSpan))
            {
                try
                {
                    await AddImgURLToListAsync(downloadedUrl, cancellationToken.Token);
                    return true;
                }

                catch (TaskCanceledException)
                {
                    MessageBox.Show("Timeout Exception", "Exception");
                    return false;
                }
            }
        }

        private async Task SavePicturesAsync(string selectedSavePath)
        {
            if (selectedSavePath == "") { MessageBox.Show($"Save canceled.", "Canceled"); return; }

            AddURLCollectionToDownloads();

            int imgCounter = 0;

            while (downloads.Count > 0)
            {
                Task<byte[]> completedTask = null;
                try
                {
                    completedTask = await Task.WhenAny(downloads.Keys);
                    string fullPath = GetFullPath(selectedSavePath, ImgName(imgCounter, ImgExtension(downloads[completedTask])));

                    using (FileStream fs = File.Open($"{fullPath}", FileMode.Create))
                    {
                        await fs.WriteAsync(completedTask.Result, 0, completedTask.Result.Length);
                        labelStatus.Invoke(new Action(() => labelStatus.Text = $"Downloaded {imgCounter + 1} of {URLCollection.Count} images"));
                        downloads.Remove(completedTask);
                        imgCounter++;
                    }
                }
                catch
                {
                    downloads.Remove(completedTask);
                    continue;
                }
            }
            MessageBox.Show($"Successfully saved {imgCounter} images.", "Success");
        }

        private async Task PrintImgURLAsync()
        {
            Task print = Task.Run(() =>
            {
                for (int i = 0; i < URLCollection.Count; i++)
                {
                    textBoxDisplayURLs.Invoke(new Action(() => textBoxDisplayURLs.Text += $"{URLCollection[i]}" + Environment.NewLine));
                    labelStatus.Invoke(new Action(() => labelStatus.Text = $"Found {i + 1} images"));
                }
            });
            await print;

            if (print.Status == TaskStatus.RanToCompletion)
            {
                buttonSave.Enabled = true;
            }
        }

        private async Task ExtractHTMLAsync(string input)
        {
            ResetApplication();
            CheckIfInputContainsHttp(input);
            var downloadedURL = await DownloadUrlAsync();

            if (downloadedURL == null) return;

            var result = await AddImgURLToListWithTimeoutAsync(TimeSpan.FromMilliseconds(10000), downloadedURL);

            if (result == true)
            {
                await PrintImgURLAsync();
            }
        }

    }
}