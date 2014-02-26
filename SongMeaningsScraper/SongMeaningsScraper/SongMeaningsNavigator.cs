using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;

namespace SongMeaningsScraper
{
    public class SongMeaningsNavigator
    {
        #region Artist directory keys
        public static readonly String[] artistDirectoryHeadings =
            { "a", "b", "c", "d", "e", 
            "f", "g", "h", "i", "j", 
            "k", "l", "m", "n", "o", 
            "p", "q", "r", "s", "t", 
            "u", "v", "w", "x", "y", 
            "z", "numbers" 
        };
        #endregion
          
        public IEnumerable<SongMeaningsSong> GetAllSongs(bool tryResumeSearch)
        {
            return GetAllSongsBeneathNode(new RootNode(tryResumeSearch));
        }
        private IEnumerable<SongMeaningsSong> GetAllSongsBeneathNode(SongMeaningsNode node)
        {
            if (node.IsTerminalNode)
            {
                yield return ((SongNode)node).Song;
            }
            else
            {
                foreach (SongMeaningsNode child in node.GetChildren())
                {
                    foreach (SongMeaningsSong song in GetAllSongsBeneathNode(child))
                    {
                        node.SerializeCurrentSearchProgress();
                        yield return song;
                    }
                }
            }
        }
    }

    abstract class SongMeaningsNode
    {
        protected readonly String _url;
        protected readonly bool _isTerminalNode;
        protected readonly bool _tryResumeSearch;
        protected const String SEARCH_SERIALIZATION_LOCATION = @"/Users/ctbailey/Documents/Research/Data/SongMeanings Lyrics/SearchState/";

        public String Url
        {
            get
            {
                return _url;
            }
        }
        public bool IsTerminalNode
        {
            get { return _isTerminalNode; }
        }
        public SongMeaningsNode(String url, bool tryResumeSearch, bool isTerminalNode = false)
        {
            _url = url;
            _isTerminalNode = isTerminalNode;
            _tryResumeSearch = tryResumeSearch;
        }
        public abstract IEnumerable<SongMeaningsNode> GetChildren();
        protected String GetTextBetweenTags(String pageSource, string startTag, string endTag)
        {
            int startTagIndex = pageSource.IndexOf(startTag);
            if (startTagIndex == -1)
            {
                return null;
            }

            int startIndex = startTagIndex + startTag.Length;
            int endIndex = pageSource.IndexOf(endTag, startIndex);
            int length = endIndex - startIndex;
            return pageSource.Substring(startIndex, length);
        }
        protected String GetPageSource(String url)
        {
            try
            {
                return GetPageSourceWithoutErrorHandling(url);
            }
            catch(System.Net.WebException ex)
            {
                System.Console.WriteLine("Exception caught: " + ex.Message);
                TimeSpan waitTime = TimeSpan.FromMinutes(5);
                DateTime resumeTime = DateTime.Now + waitTime;
                System.Console.WriteLine("Scraping scheduled to resume at " + resumeTime);
                Thread.Sleep((int)waitTime.TotalMilliseconds);
                return GetPageSourceWithoutErrorHandling(url); // If waiting for ten minutes doesn't solve the problem, let the exception go to the method caller
            }
        }
        private String GetPageSourceWithoutErrorHandling(String url)
        {
            WebRequest r = WebRequest.Create(url);
            Stream s = r.GetResponse().GetResponseStream();
            StreamReader pageReader = new StreamReader(s);
            return pageReader.ReadToEnd();
        }
        protected object TryDeserializeObjectFromXml(Type objectType, String fileName, object defaultValue)
        {
            String filePath = SEARCH_SERIALIZATION_LOCATION + fileName;
            if (File.Exists(filePath))
            {
                XmlSerializer serializer = new XmlSerializer(objectType);
                FileStream myFileStream = new FileStream(filePath, FileMode.Open);
                object deserializedObject = serializer.Deserialize(myFileStream);
                myFileStream.Close();
                return deserializedObject;
            }
            else
            {
                return defaultValue;
            }
        }
        protected void SerializeObjectToXml(Type objectType, String fileName, object obj)
        {
            XmlSerializer serializer = new XmlSerializer(objectType);

            TextWriter writer = new StreamWriter(SEARCH_SERIALIZATION_LOCATION + fileName);
            serializer.Serialize(writer, obj);
            writer.Close();
        }
        public abstract void SerializeCurrentSearchProgress();
    }
    class RootNode : SongMeaningsNode
    {
        private int _currentHeadingIndex;
        private const int DEFAULT_START_INDEX = 0;
        private const String START_INDEX_FILE_NAME = "root_node_start_index.xml";
        public RootNode(bool tryResumeSearch = false) : base(@"http://songmeanings.com/artist/directory/", tryResumeSearch){}
        public override IEnumerable<SongMeaningsNode> GetChildren()
        {
            int startIndex;
            if (_tryResumeSearch)
            {
                startIndex = GetSerializedStartIndex();
            }
            else
            {
                startIndex = DEFAULT_START_INDEX;
            }
            for(int i = startIndex; i < SongMeaningsNavigator.artistDirectoryHeadings.Length; i++)
            {
                _currentHeadingIndex = i;
                bool childShouldTryResumeSearch = (i == startIndex) && _tryResumeSearch; // First child should try and resume search
                var artistDirectoryNode = new ArtistDirectoryNode(SongMeaningsNavigator.artistDirectoryHeadings[i], childShouldTryResumeSearch);
                yield return artistDirectoryNode;
            }
        }
        public override void SerializeCurrentSearchProgress()
        {
            SerializeObjectToXml(typeof(int), START_INDEX_FILE_NAME, _currentHeadingIndex);
        }
        private int GetSerializedStartIndex()
        {
            return (int)TryDeserializeObjectFromXml(typeof(int), START_INDEX_FILE_NAME, DEFAULT_START_INDEX);
        }
    }
    class ArtistDirectoryNode : SongMeaningsNode
    {
        private const String ARTIST_LIST_START_TAG = @"<tbody>";
        private const String ARTIST_LIST_END_TAG = @"</tbody>";
        private const String ARTIST_ID_START_STRING = @"<a href=""http://songmeanings.com/artist/view/songs/";
        private const String ARTIST_ID_END_STRING = @"/""";
        private const String ARTIST_NAME_START_STRING = @"title=""";
        private const String ARTIST_END_START_STRING = @""">";
        private const String PAGE_START_FILE_NAME = @"artist_directory_starting_page.xml";
        private const String LAST_ARTIST_PROCESSED_FILE_NAME = @"last_artist_processed.xml";
        private const int DEFAULT_PAGE_TO_START_AT = 1;

        private int _currentPage;
        private SongMeaningsArtist lastArtistProcessed;

        public ArtistDirectoryNode(String directoryHeading, bool tryResumeSearch = false) : base (@"http://songmeanings.com/artist/directory/" + directoryHeading + "/", tryResumeSearch) {}
        public override IEnumerable<SongMeaningsNode> GetChildren()
        {
            bool onFirstPageOfSearch = true;
            foreach (String pageOfArtists in GetArtistListTextForAllPagesInThisDirectory())
            {
                foreach (SongMeaningsArtist artist in GetArtistsFromText(pageOfArtists, onFirstPageOfSearch)) // Only tries to resume from previous progress if on first page of search
                {
                    ArtistNode artistNode = new ArtistNode(artist);
                    lastArtistProcessed = artist;
                    yield return artistNode;
                }
                onFirstPageOfSearch = false;
            }
        }
        
        private IEnumerable<String> GetArtistListTextForAllPagesInThisDirectory()
        {
            int pageToStartAt;
            if (_tryResumeSearch)
            {
                pageToStartAt = GetSerializedPageToStartAt();
            }
            else
            {
                pageToStartAt = DEFAULT_PAGE_TO_START_AT;
            }

            for(int i = pageToStartAt; true; i++)
            {
                _currentPage = i;
                String pageUrl = this.Url + "?page=" + i;
                String artistsListText = GetTextBetweenTags(GetPageSource(pageUrl), ARTIST_LIST_START_TAG, ARTIST_LIST_END_TAG);
                if (artistsListText == null) // No artist list means we've gone past the last page of the directory
                {
                    yield break;
                }
                else
                {

                    yield return artistsListText;
                }
            }
        }
        private IEnumerable<SongMeaningsArtist> GetArtistsFromText(String artistListText, bool onFirstPageOfSearch)
        {
            String pattern = @"<a href=""[^""]+"" title=""[^""]+"">";
            MatchCollection matches = Regex.Matches(artistListText, pattern);
            List<SongMeaningsArtist> artistList = new List<SongMeaningsArtist>();
            foreach (Match m in matches)
            {
                String artistLink = m.Value;
                String artistName = GetTextBetweenTags(artistLink, ARTIST_NAME_START_STRING, ARTIST_END_START_STRING);
                String artistId = GetTextBetweenTags(artistLink, ARTIST_ID_START_STRING, ARTIST_ID_END_STRING);
                var artist = new SongMeaningsArtist(artistName, artistId);
                artistList.Add(artist);
            }

            if (onFirstPageOfSearch && _tryResumeSearch)
            {
                SongMeaningsArtist lastArtistProcessed = GetSerializedLastArtistProcessed();
                if(artistList.Contains(lastArtistProcessed))
                {
                    artistList.RemoveRange(0, artistList.IndexOf(lastArtistProcessed)); // Start over with the same artist we were working on previously, in case we didn't get all their songs
                }
            }

            foreach (SongMeaningsArtist artist in artistList)
            {
                yield return artist;
            }
        }
        private int GetSerializedPageToStartAt()
        {
            return (int)TryDeserializeObjectFromXml(typeof(int), PAGE_START_FILE_NAME, DEFAULT_PAGE_TO_START_AT);
        }
        private SongMeaningsArtist GetSerializedLastArtistProcessed()
        {
            return (SongMeaningsArtist)TryDeserializeObjectFromXml(typeof(SongMeaningsArtist), LAST_ARTIST_PROCESSED_FILE_NAME, null);
        }
        public override void SerializeCurrentSearchProgress()
        {
            SerializeObjectToXml(typeof(int), PAGE_START_FILE_NAME, _currentPage);
            SerializeObjectToXml(typeof(SongMeaningsArtist), LAST_ARTIST_PROCESSED_FILE_NAME, lastArtistProcessed);
        }
    }
    class ArtistNode : SongMeaningsNode
    {
        #region Text processing constants
        private const String SONG_LIST_START_STRING = @"<tbody id=""songslist"">";
        private const String SONG_LIST_END_STRING = @"</tbody>";
        private const String SONG_ID_START_STRING = @"<a style="""" class="""" href=""http://songmeanings.com/songs/view/";
        private const String SONG_ID_END_STRING = @"/""";
        private const String SONG_TITLE_START_STRING = @"title=""";
        private const String SONG_TITLE_END_STRING = @" lyrics"">";
        #endregion

        SongMeaningsArtist _artist;
        public ArtistNode(SongMeaningsArtist artist) : base(@"http://songmeanings.com/artist/view/songs/" + artist.artistId + "/", tryResumeSearch:false)
        {
            _artist = artist;
        }
        public override IEnumerable<SongMeaningsNode> GetChildren()
        {
            foreach (SongMeaningsSong song in GetAllSongsByArtist())
            {
                yield return new SongNode(song);
            }
        }
        private IEnumerable<SongMeaningsSong> GetAllSongsByArtist()
        {
            String pattern = @"<a style="""" class="""" href=""[^""]+"" title=""[^""]+"">";
            String songListText = GetTextBetweenTags(GetPageSource(this.Url), SONG_LIST_START_STRING, SONG_LIST_END_STRING);
            if (songListText != null)
            {
                foreach (Match artistLinkMatch in Regex.Matches(songListText, pattern))
                {
                    String songLinkTag = artistLinkMatch.Value;
                    String songId = GetTextBetweenTags(songLinkTag, SONG_ID_START_STRING, SONG_ID_END_STRING);
                    String songTitle = GetTextBetweenTags(songLinkTag, SONG_TITLE_START_STRING, SONG_TITLE_END_STRING);
                    if (!IsCommentsLink(songTitle))
                    {
                        yield return new SongMeaningsSong(_artist, songTitle, songId);
                    }
                }
            }
        }
        private bool IsCommentsLink(String linkTitle)
        {
            String commentsPattern = @"\d comments on";
            return Regex.Match(linkTitle, commentsPattern).Success;
        }
        public override void SerializeCurrentSearchProgress()
        {
            // Do nothing
        }
    }
    class SongNode : SongMeaningsNode
    {
        private const String LYRICS_START_TAG = @"<div id=""textblock"" style=""z-index: 10000;"">";
        private const string LYRICS_END_TAG = @"</div>";
        private SongMeaningsSong _song;
        public SongMeaningsSong Song
        {
            get
            {
                return _song;
            }
        }
        public SongNode(SongMeaningsSong song) : base(@"http://songmeanings.com/songs/view/" + song.songId + "/", tryResumeSearch:false, isTerminalNode:true)
        {
            if (song.Lyrics == null)
            {
                song.Lyrics = GetTextBetweenTags(GetPageSource(this.Url), LYRICS_START_TAG, LYRICS_END_TAG);
            }
            _song = song;
        }
        public override IEnumerable<SongMeaningsNode> GetChildren()
        {
            return null;
        }
        public override void SerializeCurrentSearchProgress()
        {
            // Do nothing
        }
    }
}