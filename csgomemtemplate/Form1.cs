using hazedumper;
using swed32;
using System.Diagnostics;
using System.Threading;
using skinchanger;
using System.Runtime.InteropServices;

namespace exmenu
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(Keys vKeys);

        List<IDS.saved> saved = new List<IDS.saved>(); // list to store skins

        swed swed = new swed();
        IntPtr client, engine;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var selectedWeaponId = (IDS.AllWeaponsIDs)comboBox1.SelectedValue;
            var selectedSkinId = (IDS.AllSkinIDs)comboBox2.SelectedValue;

            var newSkin = new IDS.saved
            {
                weaponid = (int)selectedWeaponId,
                skinid = (int)selectedSkinId,
                wear = float.Parse(textBox3.Text)
            };

            foreach(var item in saved.ToList())
            {
                if (item.weaponid == newSkin.weaponid)
                    saved.Remove(item); // remove old skin
            }

            saved.Add(newSkin);

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            swed.GetProcess("csgo"); // init 
            client = swed.GetModuleBase("client.dll");
            engine = swed.GetModuleBase("engine.dll");

            this.comboBox1.DataSource = Enum.GetValues(typeof(IDS.AllWeaponsIDs)); // populate with weapon ids
            this.comboBox2.DataSource = Enum.GetValues(typeof(IDS.AllSkinIDs)); // populate with skin ids

            // run loops in seperate threads
            Thread skinchangerThread = new Thread(skinchanger) { IsBackground = true };
            skinchangerThread.Start();

            Thread bhopThread = new Thread(bhop) { IsBackground = true };
            bhopThread.Start();
        }

        void skinchanger()
        {
            while(true)
            {
                var localPlayer = swed.ReadPointer(client, signatures.dwLocalPlayer);

                // loop weapon slots
                for (int i = 0; i < 3; i++)
                {
                    var currentWeapon = BitConverter.ToUInt32(swed.ReadBytes(localPlayer, netvars.m_hMyWeapons + i * 0x4, 4), 0) & 0xfff;

                    var weaponPointer = swed.ReadPointer(client, (int)(signatures.dwEntityList + (currentWeapon - 1) * 0x10)); // explicit cast to (int)

                    var weaponId = BitConverter.ToInt16(swed.ReadBytes(weaponPointer, netvars.m_iItemDefinitionIndex, 2), 0);

                    // get and apply skin
                    var setting = getSkin(weaponId);

                    if (setting != null)
                        applySkin(weaponPointer, setting);

                }

                Thread.Sleep(2);
            }
        }

        void applySkin(IntPtr entPointer, IDS.saved skinSetting)
        {
            // get current skin id
            var currentSkin = BitConverter.ToInt32(swed.ReadBytes(entPointer, netvars.m_nFallbackPaintKit, 4), 0);

            // if we dont already have desired skin
            if (currentSkin != skinSetting.skinid)
            {
               
                swed.WriteBytes(entPointer, netvars.m_iItemIDHigh, BitConverter.GetBytes(-1)); // force game to use new IDs
                swed.WriteBytes(entPointer, netvars.m_nFallbackPaintKit, BitConverter.GetBytes(skinSetting.skinid)); //apply desired skin
                swed.WriteBytes(entPointer, netvars.m_flFallbackWear, BitConverter.GetBytes(skinSetting.wear)); // skin wear float

                var clientState = swed.ReadPointer(engine, signatures.dwClientState);
                swed.WriteBytes(clientState, 0x174, BitConverter.GetBytes(-1)); // force update
            }
        }

        IDS.saved? getSkin(int currentId)
        {
            foreach(var skin in saved)
            {
                if (skin.weaponid == currentId)
                    return skin;
            }

            return null;
        }
        
        void bhop()
        {
            while (true)
            {
                if (GetAsyncKeyState(Keys.Space) < 0)
                {
                    var buffer = swed.ReadPointer(client, signatures.dwLocalPlayer);

                    // bytes: 4 is standard, 5 is +jump
                    // flag: 257 = standing, 263 = crouched, 261 = begin 

                    var flag = BitConverter.ToInt32(swed.ReadBytes(buffer, netvars.m_fFlags, 4), 0);

                    if (flag == 257 || flag == 263 || flag == 261)
                    {
                        swed.WriteBytes(client, signatures.dwForceJump, BitConverter.GetBytes(5));
                    }
                    else
                    {
                        swed.WriteBytes(client, signatures.dwForceJump, BitConverter.GetBytes(4));
                    }
                }

                Thread.Sleep(2);
            }
        }

    }
}