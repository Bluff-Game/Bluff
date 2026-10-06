namespace Bluff.Core
{
    public readonly struct ActionResult
    {
        public readonly bool Ok;

        /// <summary>Pesan untuk pemain kalau aksi ditolak (bahasa Indonesia, siap ditampilkan di UI).</summary>
        public readonly string Error;

        ActionResult(bool ok, string error)
        {
            Ok = ok;
            Error = error;
        }

        public static ActionResult Success => new ActionResult(true, null);

        public static ActionResult Fail(string error) => new ActionResult(false, error);

        public override string ToString() => Ok ? "OK" : Error;
    }
}
