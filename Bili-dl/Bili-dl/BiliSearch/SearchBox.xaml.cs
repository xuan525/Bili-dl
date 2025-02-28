﻿using Bili;
using JsonUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BiliSearch
{
    /// <summary>
    /// SearchBox.xaml 的交互逻辑
    /// Author: Xuan525
    /// Date: 24/04/2019
    /// </summary>
    public partial class SearchBox : UserControl
    {
        /// <summary>
        /// Search delegate.
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="text">Text</param>
        public delegate void SearchDel(SearchBox sender, string text);
        /// <summary>
        /// Occurs when a text ceed to be search.
        /// </summary>
        public event SearchDel Search;

        public static readonly DependencyProperty SuggestDelayProperty = DependencyProperty.Register("SuggestDelay", typeof(int), typeof(SearchBox), new FrameworkPropertyMetadata(100));
        // Suggest delay in millisecond.
        public int SuggestDelay
        {
            get
            {
                return (int)GetValue(SuggestDelayProperty);
            }
            set
            {
                SetValue(SuggestDelayProperty, value);
            }
        }
        private void SuggestDelayChanged(object sender, EventArgs e)
        {

        }

        public string Text
        {
            get
            {
                return InputBox.Text;
            }
            set
            {
                InputBox.Text = value;
            }
        }

        public SearchBox()
        {
            InitializeComponent();

            DependencyPropertyDescriptor SuggestDelayPropertyDescriptor = DependencyPropertyDescriptor.FromProperty(SuggestDelayProperty, typeof(SearchBox));
            SuggestDelayPropertyDescriptor.AddValueChanged(this, SuggestDelayChanged);
        }

        /// <summary>
        /// Class <c>SeasonSuggest</c> models the info of a Season suggestion.
        /// Author: Xuan525
        /// Date: 24/04/2019
        /// </summary>
        public class SeasonSuggest : Suggest
        {
            public string Cover;
            public string Uri;
            public long Ptime;
            public string SeasonTypeName;
            public string Area;
            public string Label;

            public SeasonSuggest(Json.Value item)
            {
                Position = item["position"];
                if (item.Contains("title"))
                    Title = item["title"];
                else
                    Title = null;
                Keyword = item["keyword"];

                // TODO: json response not contain following info any more
                Cover = "https:" + item["cover"];
                Uri = item["uri"];
                Ptime = item["ptime"];
                SeasonTypeName = item["season_type_name"];
                Area = item["area"];
                if (item.Contains("label"))
                    Label = item["label"];
                else
                    Label = null;
            }
        }

        /// <summary>
        /// Class <c>UserSuggest</c> models the info of a User suggestion.
        /// Author: Xuan525
        /// Date: 24/04/2019
        /// </summary>
        public class UserSuggest : Suggest
        {
            public string Cover;
            public string Uri;
            public uint Level;
            public long Fans;
            public long Archives;

            public UserSuggest(Json.Value item)
            {
                Position = item["position"];
                Title = item["title"];
                Keyword = item["keyword"];

                // TODO: json response not contain following info any more
                Cover = "https:" + item["cover"];
                Uri = item["uri"];
                Level = item["level"];
                Fans = item["fans"];
                Archives = item["archives"];
            }
        }

        /// <summary>
        /// Class <c>Suggest</c> models the info of a suggestion.
        /// Author: Xuan525
        /// Date: 24/04/2019
        /// </summary>
        public class Suggest
        {
            public uint Position;
            public string Title;
            public string Keyword;
            public string Type;

            public Suggest()
            {

            }

            public Suggest(Json.Value item)
            {
                Position = item["position"];

                if (item.Contains("title"))
                    Title = item["title"];
                else
                    Title = null;

                Keyword = item["keyword"];

                if (item.Contains("sug_type"))
                    Type = item["sug_type"];
                else
                    Type = null;
            }
        }

        private async void InputBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (this.IsInitialized && InputBox.IsFocused)
            {
                List<Suggest> suggests = null;
                suggests = await GetSuggestAsync(InputBox.Text, SuggestDelay);

                SuggestList.Items.Clear();
                if (suggests != null)
                {
                    SuggestList.Visibility = Visibility.Visible;
                    foreach (Suggest suggest in suggests)
                    {
                        ListBoxItem listBoxItem = new ListBoxItem();
                        listBoxItem.VerticalAlignment = VerticalAlignment.Stretch;
                        if (suggest.GetType() == typeof(Suggest))
                        {
                            listBoxItem.Content = new SuggestItem(suggest);
                            listBoxItem.Tag = suggest.Keyword;
                        }
                        else if (suggest.GetType() == typeof(SeasonSuggest))
                        {
                            SeasonSuggest seasonSuggest = (SeasonSuggest)suggest;
                            listBoxItem.Content = new SuggestItemSeason(seasonSuggest);
                            listBoxItem.Tag = seasonSuggest.Keyword;
                        }
                        else if (suggest.GetType() == typeof(UserSuggest))
                        {
                            UserSuggest userSuggest = (UserSuggest)suggest;
                            listBoxItem.Content = new SuggestItemUser(userSuggest);
                            listBoxItem.Tag = userSuggest.Keyword;
                        }
                        SuggestList.Items.Add(listBoxItem);
                    }
                }
                else
                    SuggestList.Visibility = Visibility.Hidden;
            }
            else
                SuggestList.Visibility = Visibility.Hidden;
        }

        private CancellationTokenSource cancellationTokenSource;
        private Task<List<Suggest>> GetSuggestAsync(string text, int delay)
        {
            if (cancellationTokenSource != null)
                cancellationTokenSource.Cancel();

            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            Task<List<Suggest>> task = new Task<List<Suggest>>(() =>
            {
                if (text == null || text == "")
                    return null;
                Thread.Sleep(delay);
                if (cancellationToken.IsCancellationRequested)
                    return null;
                List<Suggest> result = GetSuggest(text);
                if (cancellationToken.IsCancellationRequested)
                    return null;
                return result;
            });
            task.Start();
            return task;
        }

        private List<Suggest> GetSuggest(string text)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("highlight", "1");
            dic.Add("keyword", text);
            try
            {
                Json.Value json = BiliApi.RequestJsonResult("https://app.bilibili.com/x/v2/search/suggest3", dic, true);

                if (json["data"].Contains("list"))
                {
                    List<Suggest> suggests = new List<Suggest>();
                    foreach (Json.Value i in json["data"]["list"])
                    {
                        if (!i.Contains("term_type"))
                        {
                            Suggest suggest = new Suggest(i);
                            suggests.Add(suggest);
                        }
                        else
                        {
                            switch ((int)i["term_type"])
                            {
                                case 1:
                                    // General
                                    Suggest suggest = new Suggest(i);
                                    suggests.Add(suggest);
                                    break;
                                case 4:
                                    // User

                                    //UserSuggest userSuggest = new UserSuggest(i);     // TODO: UserSuggest is not valid any more (See constructor)
                                    Suggest userSuggest = new Suggest(i);
                                    suggests.Add(userSuggest);
                                    break;
                                case 5:
                                    // Topic
                                    Suggest topicSuggest = new Suggest(i);
                                    suggests.Add(topicSuggest);
                                    break;
                                case 8:
                                    //SeasonSuggest seasonSuggest = new SeasonSuggest(i);        // TODO: UserSuggest is not valid any more (See constructor)
                                    Suggest seasonSuggest = new Suggest(i);
                                    suggests.Add(seasonSuggest);
                                    break;
                                default:
                                    Suggest defSuggest = new Suggest(i);
                                    suggests.Add(defSuggest);
                                    break;
                            }
                        }
                    }
                    suggests.Sort((x, y) => x.Position.CompareTo(y.Position));
                    return suggests;
                }
                return null;
            }
            catch (WebException)
            {
                return null;
            }
        }

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                SuggestList.Focus();
                SuggestList.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                Confirm();
                e.Handled = true;
            }
        }

        private void SuggestList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (SuggestList.SelectedIndex < SuggestList.Items.Count - 1)
                    SuggestList.SelectedIndex++;
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                SuggestList.SelectedIndex--;
                if (SuggestList.SelectedIndex == -1)
                    InputBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                InputBox.Text = ((ListBoxItem)((ListBox)sender).SelectedItem).Tag.ToString();
                Confirm();
                e.Handled = true;
            }
            e.Handled = true;
        }

        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            InputBox.Text = ((ListBoxItem)sender).Tag.ToString();
            Confirm();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            Confirm();
        }

        private async void Confirm()
        {
            SearchBtn.Focus();
            await GetSuggestAsync("", 0);
            SuggestList.Visibility = Visibility.Hidden;
            Search?.Invoke(this, InputBox.Text);
        }
    }
}
