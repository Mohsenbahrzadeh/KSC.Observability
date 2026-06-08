using System;
using System.Threading;
using System.Web.UI;

namespace KSC.Sample.WebApp
{
    public partial class Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // A little artificial work so request-duration histograms have something to show.
            Thread.Sleep(new Random().Next(5, 80));
            TimeLabel.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
