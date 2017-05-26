namespace TSPCsharp
{
    class itemList
    {
        public itemList(double distance, int index)
        {
            this.dist = distance;
            this.index = index;
        }

        public double dist { get; set; }
        public int index { get; set; }
    }
}
