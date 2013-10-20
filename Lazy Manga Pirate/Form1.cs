using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Threading;
using System.IO;
using System.IO.Packaging;
using System.Text.RegularExpressions;

//NOTES:
//everything formatted as http://www.goodmanga.net/{Manga Name}/chapter/{Chapter}/{Page Number}
//Lies http://r1.goodmanga.net/images/manga/{Manga Name}/{Chapter}/{Page Number}.jpg
//Prolly just gonna look for 404 for end of chapter :s

namespace Lazy_Manga_Pirate
{
    public partial class Form1 : Form
    {
        HttpWebRequest client;
        List<string> titles;
        public Form1()
        {
            InitializeComponent();
            textBox2.Text = Directory.GetCurrentDirectory() + "/";
            titles = new List<string>();

        }
        #region getMangas
        private void button1_Click(object sender, EventArgs e)
        {
            updateProgressBar(0);
            client = ((HttpWebRequest)WebRequest.Create("http://www.goodmanga.net/manga-list"));
            updateProgressBar(5);
            client.BeginGetResponse(new AsyncCallback(listReceived), client);

        }
        private void listReceived(IAsyncResult result)
        {
            HttpWebResponse response = (result.AsyncState as HttpWebRequest).EndGetResponse(result) as HttpWebResponse;
            String toParse = new StreamReader(response.GetResponseStream()).ReadToEnd();
            response.Close();
            updateProgressBar(50);
            MatchCollection test = Regex.Matches(toParse, "<td><a href=\"http://www.goodmanga.net/.*\\/.*\">(?<Title>.*)<\\/a></td>", RegexOptions.IgnoreCase);
            foreach (Match m in test)
            {
                titles.Add(m.Groups["Title"].ToString());
            }
            populateListBox(titles.ToArray());
            updateProgressBar(100);

        }
        #endregion
        #region UIStuff
        private delegate void populateListBoxCallBack(string[] items);
        private delegate void updateProgressBarCallBack(int percent);
        private delegate void updateChaptersBoxCallBack(string[] chapters);

        private void populateListBox(string[] items)
        {
            if (this.listBox1.InvokeRequired)
            {
                populateListBoxCallBack c = new populateListBoxCallBack(populateListBox);
                this.Invoke(c, new object[] { items });
            }
            else
            {
                listBox1.Items.Clear();
                listBox1.Items.AddRange(items);
            }
        }
        private void updateProgressBar(int percent)
        {
            if (this.progressBar1.InvokeRequired)
            {
                updateProgressBarCallBack c = new updateProgressBarCallBack(updateProgressBar);
                this.Invoke(c, new object[] { percent });
            }
            else
            {
                progressBar1.Value = percent;
            }
        }
        private void updateChaptersBox(string[] chapters)
        {
            if (checkedListBox1.InvokeRequired)
            {
                updateChaptersBoxCallBack c = new updateChaptersBoxCallBack(updateChaptersBox);
                this.Invoke(c, new object[] { chapters });
            }
            else
            {
                checkedListBox1.Items.Clear();
                checkedListBox1.Items.AddRange(chapters);
            }
        }
        private void updateVProgressBar()
        {
            if (verticalProgressBar1.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { updateVProgressBar(); });
            }
            else
            {
                verticalProgressBar1.Value++;
                if (verticalProgressBar1.Value == verticalProgressBar1.Maximum)
                {
                    button2.Enabled = true;
                    button3.Enabled = true;
                    checkedListBox1.Enabled = true;
                }
            }
        }
        #endregion
        #region getChapters
        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                string chapterfetchuri = "http://www.goodmanga.net/" + formatMangaTitle(listBox1.SelectedItem.ToString()) + "/chapter/1";
                client = ((HttpWebRequest)WebRequest.Create(chapterfetchuri));
                client.BeginGetResponse(new AsyncCallback(fetchChapters), client);
            }
        }
        private void fetchChapters(IAsyncResult result)
        {
            HttpWebResponse response = (result.AsyncState as HttpWebRequest).EndGetResponse(result) as HttpWebResponse;
            string html = new StreamReader(response.GetResponseStream()).ReadToEnd();
            List<string> chpts =  new List<string>();
            foreach (Match m in Regex.Matches(html, "option v.+?\">(.+?(Chapter \\d{1,5}\\.\\d|Chapter \\d{1,5}))"))
            {
                chpts.Add(m.Groups[1].ToString());
            }           
            updateChaptersBox(chpts.ToArray());
            if (tabControl1.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { tabControl1.SelectedTab = tabControl1.TabPages[1]; });
            }
            else
            {
                tabControl1.SelectedTab = tabControl1.TabPages[1];
            }

        }
        private void button3_Click(object sender, EventArgs e)
        {
            Directory.CreateDirectory(listBox1.SelectedItem.ToString() + "/");
            string chapterurl = "http://t1.goodmanga.net/images/manga/" + formatMangaTitle(listBox1.SelectedItem.ToString()) + "/";
            verticalProgressBar1.Maximum = checkedListBox1.CheckedItems.Count;
            verticalProgressBar1.Value = 0;
            button2.Enabled = false;
            button3.Enabled = false; ;
            checkedListBox1.Enabled = false;
            ThreadPool.SetMaxThreads((int)numericUpDown1.Value, (int)numericUpDown1.Value);
            for (int i = 0; i < checkedListBox1.CheckedItems.Count; i++)
            {
                string temp = checkedListBox1.CheckedItems[i].ToString().Trim();
                temp = temp.Substring(temp.LastIndexOf(' ') + 1);
                ThreadPool.QueueUserWorkItem(new WaitCallback(downloadChapter), new object[] { chapterurl + temp + "/", listBox1.SelectedItem.ToString(), temp });
            }
        }
        private void downloadChapter(object param1)
        {
            object[] param = (object[])param1;
            string uri = (string)param[0];
            string manga = (string)param[1];
            int chapterNumber = Convert.ToInt32((string)param[2]);
            int page = 1;
            if (!File.Exists(textBox2.Text + manga + "/" + manga + " Chapter " + chapterNumber.ToString("000") + ".cbz"))
            {
                using (Package zip = Package.Open(textBox2.Text + manga + "/" + manga + " Chapter " + chapterNumber.ToString("000") + ".cbz", FileMode.OpenOrCreate))
                {
                    try
                    {
                        while (true)
                        {
                            WebRequest dl = WebRequest.Create(uri + page + ".jpg");
                            WebResponse res = dl.GetResponse();
                            string temp = page.ToString("000") + ".png";
                            Uri pageUri = PackUriHelper.CreatePartUri(new Uri(page.ToString("000") + ".png", UriKind.Relative));
                            PackagePart part = zip.CreatePart(pageUri, "", CompressionOption.Normal);
                            Image.FromStream(res.GetResponseStream()).Save(part.GetStream(), System.Drawing.Imaging.ImageFormat.Png);
                            res.Close();
                            page++;
                        }
                    }
                    catch
                    {
                    }
                }
                updateVProgressBar();
            }
            //Might add error handling later....
            //Might not
        }
        #endregion

        private void button2_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
                checkedListBox1.SetItemChecked(i, true);
        }
        private string formatMangaTitle(string unformated)
        {
            if (unformated.Contains('/') || unformated.Contains('.') || unformated.Contains('+') || unformated.Contains('[') || unformated.Contains('%') || unformated.Contains('('))
            {
                return unformated.ToLower().Trim().Replace(' ', '-').Replace(".", "").Replace("/", "").Replace("+", "").Replace("\\", "").Replace("[", "").Replace("]", "");
            }
            else
            {
                return unformated.ToLower().Trim().Replace(' ', '_');
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            foreach (string s in titles)
            {
                if (s.ToString().ToLower().Contains(((TextBox)sender).Text.ToLower()))
                {
                    listBox1.Items.Add(s);
                }
            }
        }

        private void textBox2_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            folderBrowserDialog1.ShowDialog();
            if(Directory.Exists(folderBrowserDialog1.SelectedPath))
                textBox2.Text = folderBrowserDialog1.SelectedPath;
        }
    }
}
