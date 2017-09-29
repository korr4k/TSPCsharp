namespace TSPCsharp
{
    class itemList
    {
        public itemList(double distance, int index)
        {
            this.distance = distance;
            this.index = index;
        }

        internal double distance { get; set; }
        internal int index { get; set; }
    }
}
