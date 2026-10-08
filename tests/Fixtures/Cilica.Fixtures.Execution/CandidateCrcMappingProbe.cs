namespace Cilica.Fixtures.Execution
{
    public static class CandidateCrcMappingProbe
    {
        // Independently authored shape reduction; not a copied CRC implementation.
        public static int UnsignedTable(int input,int mode)
        {
            var table=new uint[4096];
            table[mode&4095]=unchecked((uint)input);
            return unchecked((int)(table[mode&4095]>>1));
        }
    }
}
