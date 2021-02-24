using System.Text;
using System.Windows.Forms;

namespace LanChat
{
    public static class Utils
    {
        public static string CreateMD5(string input)
        {           
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                return CreateMD5(inputBytes);             
            }
        }
        public static string CreateMD5(byte[] input)
        {            
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(input);
             
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }
        public static ListViewItem FirstSelected(this ListView item)
        {
            if (item.SelectedItems.Count > 0)
            {
                return item.SelectedItems[0];
            }
            return null;
        }

    }
}
