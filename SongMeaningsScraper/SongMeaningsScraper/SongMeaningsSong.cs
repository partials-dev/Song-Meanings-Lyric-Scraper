using System;

namespace SongMeaningsScraper
{
    [Serializable]
    public class SongMeaningsSong
    {
        public SongMeaningsArtist artist;
        public String songName;
        public String songId;

        private String _lyrics;
        public String Lyrics
        {
            get
            {
                return _lyrics;
            }
            set
            {
                _lyrics = value;
            }
        }

        public SongMeaningsSong(){}
        public SongMeaningsSong(SongMeaningsArtist artist, String songName, String songId, String lyrics = null)
        {
            this.artist = artist;
            this.songName = songName;
            this.songId = songId;
            this._lyrics = lyrics;
        }
        public override bool Equals(object obj)
        {
            if (obj is SongMeaningsSong)
            {
                SongMeaningsSong otherSong = (SongMeaningsSong)obj;
                return otherSong.songId.Equals(this.songId);
            }
            else
            {
                return false;
            }
        }
        public override int GetHashCode()
        {
            return songId.GetHashCode();
        }
    }
}

