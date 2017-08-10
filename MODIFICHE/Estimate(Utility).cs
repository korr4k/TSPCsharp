static int Estimate(double sample)
        {
            int k = 1;
            while (sample*k < 100)
            {
                k = k * 10;
            }
            return k;
        }
