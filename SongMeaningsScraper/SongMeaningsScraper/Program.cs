using System;
using System.Net;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace SongMeaningsScraper
{
    class MainClass
    {

        public static void Main(string[] args)
        {
            Scraper s = new Scraper();
            s.ScrapeAndSaveAllLyrics();
        }
    }
    class Scraper
    {
        #region Analytics variables
        private long songsRetrievedThisSession = 0;
        private long totalSongsRetrieved;
        private DateTime startTime;
        private String currentArtistName;
        #endregion

        #region Serialization constants
        private const String SAVE_LOCATION = @"/Users/ctbailey/Documents/Research/Data/SongMeanings Lyrics/";
        #endregion

        public void ScrapeAndSaveAllLyrics()
        {
            totalSongsRetrieved = Directory.GetFiles(SAVE_LOCATION).Count();
            startTime = DateTime.Now;
            SongMeaningsNavigator navigator = new SongMeaningsNavigator();
            foreach (SongMeaningsSong song in navigator.GetAllSongs(tryResumeSearch:true))
            {
                if (song != null)
                {
                    SerializeSong(song);
                    songsRetrievedThisSession++;
                    totalSongsRetrieved++;
                    currentArtistName = song.artist.name;
                }

                UpdateDisplay();

                Thread.Sleep(300);
            }
        }
        private void UpdateDisplay()
        {
            System.Console.Clear();
            System.Console.WriteLine("Retrieved the lyrics for " + totalSongsRetrieved + " songs.");
            System.Console.WriteLine("Currently working on songs by: " + currentArtistName);
            double averageSecondsPerSong = CalculateAverageSecondsPerSong();
            System.Console.WriteLine("Average seconds/song this session: " + averageSecondsPerSong);
            System.Console.WriteLine("Time remaining 'til a million songs have been scraped: " + TimeTilAMillion(averageSecondsPerSong));
        }
        private double CalculateAverageSecondsPerSong()
        {
            TimeSpan timeSinceStart = DateTime.Now - startTime;
            return timeSinceStart.TotalSeconds / songsRetrievedThisSession;
        }
        private TimeSpan TimeTilAMillion(double averageSecondsPerSong)
        {
            if (averageSecondsPerSong > double.MaxValue)
            {
                return TimeSpan.MaxValue;
            }
            else
            {
                long songsLeftUntilAMillion = 1000000 - totalSongsRetrieved;
                return TimeSpan.FromSeconds(songsLeftUntilAMillion * averageSecondsPerSong);
            }
        }
            
        private static void SerializeSong(SongMeaningsSong song)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(SongMeaningsSong));

            TextWriter writer = new StreamWriter(SAVE_LOCATION + "songmeanings" + song.songId + ".xml");
            serializer.Serialize(writer, song);
            writer.Close();
        }


    }


}
