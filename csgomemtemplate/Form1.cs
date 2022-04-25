using hazedumper;
using memory32;
using itemIDs;
using System.Runtime.InteropServices;

namespace exmenu
{
    public partial class Form1 : Form
    {
        // import windows functions
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(Keys vKeys);

        List<IDs.Saved> saved = new(); // list to store skins

        Memory mem = new();
        IntPtr client, engine;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            mem.GetProcess("csgo"); // init 
            client = mem.GetModuleBase("client.dll");
            engine = mem.GetModuleBase("engine.dll");

            this.comboBox1.DataSource = Enum.GetValues(typeof(IDs.AllWeaponsIDs)); // populate with weapon ids
            this.comboBox2.DataSource = Enum.GetValues(typeof(IDs.AllSkinIDs)); // populate with skin ids

            // run loops in seperate threads
            Thread skinchangerThread = new Thread(skinchanger) { IsBackground = true };
            skinchangerThread.Start();

            Thread bhopThread = new Thread(bhop) { IsBackground = true };
            bhopThread.Start();
          
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var selectedWeaponId = (IDs.AllWeaponsIDs)comboBox1.SelectedValue;
            var selectedSkinId = (IDs.AllSkinIDs)comboBox2.SelectedValue;

            var newSkin = new IDs.Saved
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

        void skinchanger()
        {
            while(true)
            {
                var localPlayer = mem.ReadPointer(client, signatures.dwLocalPlayer);

                // loop weapon slots
                for (int i = 0; i < 3; i++)
                {
                    var currentWeapon = BitConverter.ToUInt32(mem.ReadBytes(localPlayer, netvars.m_hMyWeapons + i * 0x4, 4), 0) & 0xfff;

                    var weaponPointer = mem.ReadPointer(client, (int)(signatures.dwEntityList + (currentWeapon - 1) * 0x10)); // explicit cast to (int)

                    var weaponId = BitConverter.ToInt16(mem.ReadBytes(weaponPointer, netvars.m_iItemDefinitionIndex, 2), 0);

                    // get and apply skin
                    var setting = getSkin(weaponId);

                    if (setting != null)
                        applySkin(weaponPointer, setting);

                }

                Thread.Sleep(2);
            }
        }

        void applySkin(IntPtr entPointer, IDs.Saved skinSetting)
        {
            // get current skin id
            var currentSkin = BitConverter.ToInt32(mem.ReadBytes(entPointer, netvars.m_nFallbackPaintKit, 4), 0);

            // if we dont already have desired skin
            if (currentSkin != skinSetting.skinid)
            {
               
                mem.WriteBytes(entPointer, netvars.m_iItemIDHigh, BitConverter.GetBytes(-1)); // force game to use new IDs
                mem.WriteBytes(entPointer, netvars.m_nFallbackPaintKit, BitConverter.GetBytes(skinSetting.skinid)); //apply desired skin
                mem.WriteBytes(entPointer, netvars.m_flFallbackWear, BitConverter.GetBytes(skinSetting.wear)); // skin wear float

                var clientState = mem.ReadPointer(engine, signatures.dwClientState);
                mem.WriteBytes(clientState, 0x174, BitConverter.GetBytes(-1)); // force update
            }
        }

        IDs.Saved? getSkin(int currentId)
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
                if (GetAsyncKeyState(Keys.Space) < 0 && checkBox1.Checked)
                {
                    var buffer = mem.ReadPointer(client, signatures.dwLocalPlayer);

                    // bytes: 4 is "-jump", 5 is "+jump"
                    // flag: 257 = standing, 263 = crouched, 261 = begin crouching

                    var flag = BitConverter.ToInt32(mem.ReadBytes(buffer, netvars.m_fFlags, 4), 0);
                   
                    if (flag == 257 || flag == 263 || flag == 261)
                    {
                        mem.WriteBytes(client, signatures.dwForceJump, BitConverter.GetBytes(5));
                    }
                    else if (flag == 256) // maybe resetting in air will help?
                    {
                        mem.WriteBytes(client, signatures.dwForceJump, BitConverter.GetBytes(4));
                        mem.WriteBytes(client, signatures.dwForceJump, BitConverter.GetBytes(5));
                        mem.WriteBytes(client, signatures.dwForceJump, BitConverter.GetBytes(4));
                    }
                    else
                    {
                        mem.WriteBytes(client, signatures.dwForceJump, BitConverter.GetBytes(4));
                    }
                }

                Thread.Sleep(10); // tested: "1 - 10" = misser, "20" = misser MEGET
            }
        }

    }
}