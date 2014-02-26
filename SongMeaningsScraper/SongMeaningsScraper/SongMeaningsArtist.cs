using System;

namespace SongMeaningsScraper
{
    [Serializable]
    public class SongMeaningsArtist
    {
        public String name;
        public String artistId;
        public SongMeaningsArtist(){}
        public SongMeaningsArtist(String name, String artistId)
        {
            this.name = name;
            this.artistId = artistId;
        }
        public override bool Equals(object obj)
        {
            if (obj is SongMeaningsArtist)
            {
                SongMeaningsArtist otherArtist = (SongMeaningsArtist)obj;
                return this.artistId.Equals(otherArtist.artistId);
            }
            else
            {
                return false;
            }
        }
        public override int GetHashCode()
        {
            return artistId.GetHashCode();
        }
    }
}

