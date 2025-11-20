using UnityEngine;

namespace RoomGen
{

    public enum PointType
    {
        Default,
        Wall,
        Floor,
        Door,
        Window,
        Character,
        Roof
    }


    public class DecoratorPoint
    {


        public GameObject tileObject;
        public GameObject currentDecoration;
        public Vector3 point;
        public PointType pointType;
        public bool occupied;
        public int levelNumber;

        public DecoratorPoint(GameObject tileObject_, GameObject currentDecoration_, Vector3 point_, PointType pointType_, bool occupied_, int levelNumber_)
        {
            tileObject = tileObject_;
            currentDecoration = currentDecoration_;
            point = point_;
            pointType = pointType_;
            occupied = occupied_;
            levelNumber = levelNumber_;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is DecoratorPoint other)
            {
                // Compare the point, pointType, and levelNumber so that floor and roof points are not considered duplicates.
                return point.Equals(other.point) && pointType == other.pointType;
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + point.GetHashCode();
                hash = hash * 23 + pointType.GetHashCode();
                return hash;
            }
        }


    }

}