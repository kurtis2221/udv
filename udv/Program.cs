using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Media;
using System.Drawing.Imaging;

class Form1 : Form
{
    //Screen width, length
    const short SC_W = 320;
    const short SC_H = 200;

    //Background color
    const byte BG_R = 0;
    const byte BG_G = 0;
    const byte BG_B = 65;

    //Border color
    const byte BD_R = 134;
    const byte BD_G = 40;
    const byte BD_B = 199;

    //Char size
    const byte CH_W = 8;
    const byte CH_H = 16;

    //Top text limit, color speed
    const byte TT_SD = 3;
    const byte TT_LM = 40;

    //Wave text color
    const byte WV_R = 0;
    const byte WV_G = 0;
    const byte WV_B = 255;

    //Wave
    const byte WV_LM = 41;
    const short WV_MD = 159; //Middle point - ((133+199)/2-8)+1

    //Text pointers
    const short PTR_TT = 320;
    const short PTR_WV = 826;
    const short LEN_WV = PTR_WV - PTR_TT;

    //Top text color changing
    byte[] rgb;
    byte tt_idx;
    bool tt_md;

    //Wave chars, text idx, wavelength helper, movement smooth
    byte[] wv_txt;
    short wv_idx;
    byte wv_wl;
    byte wv_mov;

    byte[] fnt_idx =
    {
        0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C,
	    0x4D, 0x4E, 0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
	    0x59, 0x5A, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30,
	    0x21, 0x3F, 0x2E, 0x3A, 0x2C, 0x28, 0x29, 0x20
    };
    byte[] fonts;
    byte[] text;

    //Image draw
    Bitmap img;
    BitmapData data;
    int stride;

    //Draw timer
    Timer tmr;

    //Music
    SoundPlayer snd;

    //Upscaler gfx - nearest mode
    Graphics gfx;

    //Scaling
    float ps_x, ps_y, ps_w, ps_h;

    public Form1()
    {
        int i, i2;
        //Form
        Text = "UDV";
        FormBorderStyle = FormBorderStyle.None;
        //Size = new Size(SC_W, SC_H);
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint, true);
        Bounds = Screen.PrimaryScreen.Bounds;
        //Data
        img = new Bitmap(SC_W, SC_H);
        //Get scale data
        if ((float)SC_W / SC_H > (float)Width / Height)
        {
            ps_x = (float)Width / SC_W;
            ps_w = Width;
            ps_h = ps_x * SC_H;
            ps_y = (Height - (SC_H * ps_x)) / 2;
            ps_x = 0;
        }
        else
        {
            ps_y = (float)Height / SC_H;
            ps_w = ps_y * SC_W;
            ps_h = Height;
            ps_x = (Width - (SC_W * ps_y)) / 2;
            ps_y = 0;
        }
        BinaryReader br = new BinaryReader(
            new FileStream("fonts", FileMode.Open, FileAccess.Read),
            Encoding.Default);
        fonts = br.ReadBytes(5632);
        br.Close();
        br = new BinaryReader(
            new FileStream("data", FileMode.Open, FileAccess.Read),
            Encoding.Default);
        text = br.ReadBytes(PTR_WV);
        br.Close();
        //Top text
        rgb = new byte[3];
        rgb[0] = BG_R;
        rgb[1] = BG_G;
        rgb[2] = BG_B;
        tt_md = false;
        tt_idx = 1;
        //Wave
        wv_txt = new byte[WV_LM];
        for (i = 0; i < WV_LM; i++)
            wv_txt[i] = 0x20;
        wv_idx = 0;
        wv_mov = 0;
        //Lock bits
        data = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        stride = data.Stride;
        //Draw screen
        for (i2 = 0; i2 < SC_H; i2++)
            for (i = 0; i < SC_W; i++)
                SetPixel(i, i2, BG_R, BG_G, BG_B);
        //Draw borders
        for (i = 0; i < SC_W; i++)
        {
            SetPixel(i, 0, BD_R, BD_G, BD_B);
            SetPixel(i, 133, BD_R, BD_G, BD_B);
            SetPixel(i, SC_H - 1, BD_R, BD_G, BD_B);
        }
        BackColor = Color.FromArgb(0, 0, 0);
        //Unlock bits
        img.UnlockBits(data);
        //Draw timer
        tmr = new Timer();
        tmr.Interval = 10; //100 FPS
        tmr.Tick += Tick;
        tmr.Start();
        snd = new SoundPlayer("wave");
        snd.PlayLooping();
    }

    void Tick(object sender, EventArgs e)
    {
        int i, i2;
        //Change colors
        if (tt_md)
        {
            rgb[tt_idx] -= TT_SD;
            if (rgb[tt_idx] <= 0 || rgb[tt_idx] >= 253) //Overflow protection
            {
                rgb[tt_idx] = 0;
                tt_md = !tt_md;
                tt_idx++;
                tt_idx %= 3;
            }
        }
        else
        {
            rgb[(tt_idx + 1) % 3] += TT_SD;
            if (rgb[(tt_idx + 1) % 3] >= 253) //Overflow protection
            {
                rgb[(tt_idx + 1) % 3] = 255;
                tt_md = !tt_md;
            }
        }
        //Lock bits
        data = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        stride = data.Stride;
        //Draw updated colored text
        for (i = 0; i < PTR_TT; i++)
            Draw(text[i], CH_W * (i % TT_LM), 3 + CH_H * (i / TT_LM));
        //Clear zone
        for (i2 = 134; i2 < 199; i2++)
            for (i = 0; i < SC_W; i++)
                SetPixel(i, i2, BG_R, BG_G, BG_B);
        //Display wave - float cast for smoothing
        for (i = 0; i < WV_LM * CH_W; i++)
            Draw2(wv_txt[i / CH_W], i - wv_mov, WV_MD + (short)(Math.Sin((wv_wl + ((float)wv_mov / 12) + (float)i / 20) / Math.PI) * 26), i % CH_W);
        wv_mov += 2;
        if (wv_mov >= CH_W)
        {
            for (i = 1; i < WV_LM; i++)
                wv_txt[i - 1] = wv_txt[i];
            wv_txt[WV_LM - 1] = text[PTR_TT + wv_idx];
            wv_mov = 0;
            wv_idx++;
            wv_idx %= LEN_WV; //Wave text idx
            wv_wl++;
            wv_wl %= 20; //Wavelength
        }
        //Unlock bits
        img.UnlockBits(data);
        //Draw scaled image
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        gfx = e.Graphics;
        gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        gfx.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
        gfx.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
        gfx.DrawImage(img, ps_x, ps_y, ps_w, ps_h);
        base.OnPaint(e);
    }

    //Font drawing
    void Draw(byte input, int x, int y)
    {
        int i;
        for (i = 0; i < fnt_idx.Length; i++)
            if (fnt_idx[i] == input)
                goto skip;
        return;
    skip:
        int i2, tmp;
        tmp = i * CH_W;
        for (i = 0; i < CH_W; i++)
            for (i2 = 0; i2 < CH_H; i2++)
                if (fonts[352 * i2 + tmp + i] == 0xF8)
                    SetPixel(x + i, y + i2, rgb[0], rgb[1], rgb[2]);
    }

    void Draw2(byte input, int x, int y, int a)
    {
        int i;
        for (i = 0; i < fnt_idx.Length; i++)
            if (fnt_idx[i] == input)
                goto skip;
        return;
    skip:
        int tmp;
        tmp = i * CH_W;
        for (i = 0; i < CH_H; i++)
            if (fonts[352 * i + tmp + a] == 0xF8)
                if (x > 0 && x < SC_W)
                    SetPixel(x, y + i, WV_R, WV_G, WV_B);
    }

    //Very fast drawing speed, CPU friendly
    unsafe void SetPixel(int x, int y, byte r, byte g, byte b)
    {
        byte* ptr = (byte*)data.Scan0;
        ptr[(x * 3) + y * stride] = b;
        ptr[(x * 3) + y * stride + 1] = g;
        ptr[(x * 3) + y * stride + 2] = r;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) Application.Exit();
        return false;
    }
}

static class Program
{
    static void Main()
    {
        Application.Run(new Form1());
    }
}