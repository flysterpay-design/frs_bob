using System;
using System.ComponentModel;
using System.Windows.Forms;
using RSBot.Core;
using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Objects;
using RSBot.Lure.Components;
using SDUI.Controls;

namespace RSBot.Lure.Views;

[ToolboxItem(false)]
public partial class Main : DoubleBufferedControl
{
    private const int ScriptRecorderOwnerId = 1000;

    private bool _configLocked;

    // Новые элементы UI
    private SDUI.Controls.CheckBox checkStopTotalMonsters;
    private SDUI.Controls.NumUpDown numTotalMonstersCount;

    public Main()
    {
        InitializeComponent();
        InitializeCustomConditions(); // Добавляем новые элементы
        SubscribeEvents();
    }

    private void InitializeCustomConditions()
    {
        // Чекбокс
        checkStopTotalMonsters = new SDUI.Controls.CheckBox
        {
            AutoSize = true,
            BackColor = System.Drawing.Color.Transparent,
            Depth = 0,
            Location = new System.Drawing.Point(30, 420),
            Margin = new System.Windows.Forms.Padding(0),
            MouseLocation = new System.Drawing.Point(-1, -1),
            Name = "checkStopTotalMonsters",
            Ripple = true,
            Size = new System.Drawing.Size(190, 30),
            TabIndex = 32,
            Text = "Stop if total monsters >=",
            UseVisualStyleBackColor = false
        };
        checkStopTotalMonsters.CheckedChanged += OnSettingsChanged;

        // NumericUpDown
        numTotalMonstersCount = new SDUI.Controls.NumUpDown
        {
            BackColor = System.Drawing.Color.Transparent,
            Font = new System.Drawing.Font("Segoe UI", 9.25F),
            ForeColor = System.Drawing.Color.FromArgb(0, 0, 0),
            Location = new System.Drawing.Point(257, 420),
            Margin = new System.Windows.Forms.Padding(3, 4, 3, 4),
            Maximum = 100,
            Minimum = 1,
            MinimumSize = new System.Drawing.Size(91, 33),
            Name = "numTotalMonstersCount",
            Size = new System.Drawing.Size(91, 33),
            TabIndex = 33,
            Value = LureConfig.TotalMonstersCount
        };
        numTotalMonstersCount.ValueChanged += OnSettingsChanged;

        // Добавляем в groupBox3
        groupBox3.Controls.Add(checkStopTotalMonsters);
        groupBox3.Controls.Add(numTotalMonstersCount);
    }

    private void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnSaveScript", new Action<int, string>(OnSaveScript));
    }

    private void OnSaveScript(int ownerId, string path)
    {
        if (IsDisposed || Disposing)
            return;

        if (ownerId != ScriptRecorderOwnerId)
            return;

        radioUseScript.Checked = true;
        txtScriptPath.Text = path;
        LureConfig.SelectedScriptPath = txtScriptPath.Text;
    }

    private void OnLoadCharacter()
    {
        if (IsDisposed || Disposing || !Game.Ready)
            return;

        checkUseHowlingShout.Enabled = Game.Player.Race == ObjectCountry.Europe;
        checkNoHowlingAtCenter.Enabled = Game.Player.Race == ObjectCountry.Europe;

        LoadConfig();
    }

    private void LoadConfig()
    {
        if (_configLocked)
            return;

        _configLocked = true;

        lblX.Text = LureConfig.Area.Position.X.ToString("0.0");
        lblY.Text = LureConfig.Area.Position.Y.ToString("0.0");
        numRadius.Value = LureConfig.Area.Radius;
        radioUseScript.Checked = LureConfig.UseScript;
        radioWalkRandomly.Checked = LureConfig.WalkRandomly;
        radioStayAtCenter.Checked = LureConfig.StayAtCenter;
        checkStayAtCenter.Checked = LureConfig.StayAtCenterFor;
        numStayAtCenterSeconds.Value = LureConfig.StayAtCenterForSeconds;
        txtScriptPath.Text = LureConfig.SelectedScriptPath;
        checkUseHowlingShout.Checked = LureConfig.UseHowlingShout;
        checkUseNormalAttack.Checked = LureConfig.UseNormalAttack;
        checkStopPartyMemberDead.Checked = LureConfig.StopIfNumPartyMemberDead;
        checkStopMonsterType.Checked = LureConfig.StopIfNumMonsterType;
        checkStopPartyMember.Checked = LureConfig.StopIfNumPartyMember;
        checkNumPartyMembersOnSpot.Checked = LureConfig.StopIfNumPartyMembersOnSpot;
        numPartyMemberDead.Value = LureConfig.NumPartyMemberDead;
        numMonsterType.Value = LureConfig.NumMonsterType;
        numPartyMember.Value = LureConfig.NumPartyMember;
        numPartyMembersOnSpot.Value = LureConfig.NumPartyMembersOnSpot;
        checkNoHowlingAtCenter.Checked = LureConfig.NoHowlingAtCenter;
        checkUseAttackingSkills.Checked = LureConfig.UseAttackingSkills;
        txtWalkbackScript.Text = LureConfig.WalkscriptPath;
        comboMonsterType.SelectedIndex = LureConfig.SelectedMonsterType switch
        {
            MonsterRarity.General => 0,
            MonsterRarity.Champion => 1,
            MonsterRarity.Giant => 2,
            MonsterRarity.GeneralParty => 3,
            MonsterRarity.ChampionParty => 4,
            MonsterRarity.GiantParty => 5,
            MonsterRarity.Elite => 6,
            MonsterRarity.EliteStrong => 7,
            MonsterRarity.Unique => 8,
            MonsterRarity.Event => 9,
            _ => 0,
        };

        // Новые настройки
        checkStopTotalMonsters.Checked = LureConfig.StopIfTotalMonsters;
        numTotalMonstersCount.Value = LureConfig.TotalMonstersCount;

        _configLocked = false;
    }

    private void OnSettingsChanged(object sender, EventArgs e)
    {
        if (_configLocked)
            return;

        _configLocked = true;

        LureConfig.Area = new Area
        {
            Name = "Lure",
            Position = LureConfig.Area.Position,
            Radius = (int)numRadius.Value,
        };

        LureConfig.UseNormalAttack = checkUseNormalAttack.Checked;
        LureConfig.UseHowlingShout = checkUseHowlingShout.Checked;
        LureConfig.StopIfNumMonsterType = checkStopMonsterType.Checked;
        LureConfig.StopIfNumPartyMemberDead = checkStopPartyMemberDead.Checked;
        LureConfig.StopIfNumPartyMember = checkStopPartyMember.Checked;
        LureConfig.StopIfNumPartyMembersOnSpot = checkNumPartyMembersOnSpot.Checked;
        LureConfig.StayAtCenterFor = checkStayAtCenter.Checked;
        LureConfig.UseScript = radioUseScript.Checked;
        LureConfig.WalkRandomly = radioWalkRandomly.Checked;
        LureConfig.StayAtCenter = radioStayAtCenter.Checked;
        LureConfig.NumMonsterType = (int)numMonsterType.Value;
        LureConfig.NumPartyMember = (int)numPartyMember.Value;
        LureConfig.NumPartyMemberDead = (int)numPartyMemberDead.Value;
        LureConfig.NumPartyMembersOnSpot = (int)numPartyMembersOnSpot.Value;
        LureConfig.StayAtCenterForSeconds = (int)numStayAtCenterSeconds.Value;
        LureConfig.NoHowlingAtCenter = checkNoHowlingAtCenter.Checked;
        LureConfig.UseAttackingSkills = checkUseAttackingSkills.Checked;

        // Новые настройки
        LureConfig.StopIfTotalMonsters = checkStopTotalMonsters.Checked;
        LureConfig.TotalMonstersCount = (int)numTotalMonstersCount.Value;

        _configLocked = false;
    }

    private void btnSetCenter_Click(object sender, EventArgs e)
    {
        LureConfig.Area = new Area
        {
            Name = "Lure",
            Position = Game.Player.Position,
            Radius = (int)numRadius.Value,
        };

        lblX.Text = LureConfig.Area.Position.X.ToString("0.0");
        lblY.Text = LureConfig.Area.Position.Y.ToString("0.0");
    }

    private void btnBrowse_Click(object sender, EventArgs e)
    {
        var fileBrowser = new OpenFileDialog
        {
            Title = "RSBot - Choose lure script",
            Filter = "RSBot script (*.rbs)|*.rbs",
            Multiselect = false,
        };

        if (fileBrowser.ShowDialog() != DialogResult.OK)
            return;

        radioUseScript.Checked = true;
        txtScriptPath.Text = fileBrowser.FileName;
        LureConfig.SelectedScriptPath = fileBrowser.FileName;
    }

    private void linkRecord_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        if (!ScriptManager.Running)
            EventManager.FireEvent("OnShowScriptRecorder", ScriptRecorderOwnerId, true);
        else
            MessageBox.Show(
                "Can not record a new script while a script is running! Stop the bot and try again.",
                "Script manager busy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
    }

    private void btnBrowseWalkscript_Click(object sender, EventArgs e)
    {
        var fileBrowser = new OpenFileDialog
        {
            Title = "RSBot - Choose walkback script",
            Filter = "RSBot script (*.rbs)|*.rbs",
            Multiselect = false,
        };

        if (fileBrowser.ShowDialog() != DialogResult.OK)
            return;

        txtWalkbackScript.Text = fileBrowser.FileName;
        LureConfig.WalkscriptPath = fileBrowser.FileName;
    }

    private void comboMonsterType_SelectedIndexChanged(object sender, EventArgs e)
    {
        LureConfig.SelectedMonsterType = comboMonsterType.SelectedIndex switch
        {
            0 => MonsterRarity.General,
            1 => MonsterRarity.Champion,
            2 => MonsterRarity.Giant,
            3 => MonsterRarity.GeneralParty,
            4 => MonsterRarity.ChampionParty,
            5 => MonsterRarity.GiantParty,
            6 => MonsterRarity.Elite,
            7 => MonsterRarity.EliteStrong,
            8 => MonsterRarity.Unique,
            9 => MonsterRarity.Event,
            _ => LureConfig.SelectedMonsterType,
        };
    }

    /// <summary>
    ///     Occurs before Main form is displayed.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Main_Load(object sender, EventArgs e)
    {
        OnLoadCharacter();
    }
}
