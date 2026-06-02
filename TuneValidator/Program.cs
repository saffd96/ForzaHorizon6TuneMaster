// Validation harness — mirrors TuneGeneratorService formulas after all three fixes:
// 1. Spring constant 0.002012 → 0.02012
// 2. Rally suspension double-soft: offRoadDisc ? 0.85 : 0.55
// 3. ARB wdAdj/pwrAdj added after wbFactor

using System;
using System.Collections.Generic;
using System.Linq;

static class V
{
    static int pass, fail;

    static void Check(string label, double val, double min, double max, string note = "")
    {
        bool ok = val >= min && val <= max;
        string s = ok ? "  OK " : "FAIL ";
        if (!ok) fail++; else pass++;
        string n = note.Length > 0 ? $"  ← {note}" : "";
        Console.WriteLine($"  {s} {label,-28} {val,8:F2}  [{min:G4}–{max:G4}]{n}");
    }

    static double Cl(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
    static double EWD(double wd, EP ep) => Math.Abs(wd - 50) > 2 ? wd
        : ep switch { EP.Front => 55, EP.Mid => 48, EP.Rear => 40, _ => 50 };

    enum DT  { RWD, FWD, AWD }
    enum EP  { Front, Mid, Rear }
    enum Dis { Road, Touge, Rally, CC, Drift, Drag, Street }
    enum PT  { ICE, Hybrid, Electric }
    enum Asp { Natural, SingleTurbo, TwinTurbo }
    enum Su  { Race, Sport, Street, Rally, Drift, Stock }
    enum Di  { Stock, Street, Sport, Race }

    static (double D, double S, double Dam) PDF(PT pt, Asp asp)
    {
        if (pt == PT.Electric) return (1.20, 1.08, 1.06);
        var (d, s, dm) = asp switch
        {
            Asp.SingleTurbo => (1.10, 1.05, 1.04),
            Asp.TwinTurbo   => (1.07, 1.04, 1.03),
            _               => (1.00, 1.00, 1.00)
        };
        if (pt == PT.Hybrid) { d=1+(d-1)*.6; s=1+(s-1)*.6; dm=1+(dm-1)*.6; }
        return (d, s, dm);
    }

    record Cfg(
        string Name, double Mass, double HP, int MaxRPM, double MaxKmh,
        int Gears, double WDF, EP EP, DT DT, Dis Dis,
        PT PT = PT.ICE, Asp Asp = Asp.Natural,
        Su Su = Su.Sport, Di Di = Di.Sport,
        double WB = 2700,
        double FRimD = 19, double FTW = 235, double FTP = 40,
        double RRimD = 19, double RTW = 265, double RTP = 35
    );

    static void Run(Cfg c)
    {
        Console.WriteLine($"\n{new string('=', 70)}");
        Console.WriteLine($"  {c.Name}");
        Console.WriteLine($"  {c.Mass}kg | {c.HP}HP | {c.MaxRPM}rpm | {c.MaxKmh}km/h | {c.Gears}spd | {c.DT} | {c.PT}/{c.Asp} | {c.Su} susp");
        Console.WriteLine(new string('=', 70));

        double wd  = EWD(c.WDF, c.EP);
        double wdF = wd / 100.0, wdR = 1 - wdF;
        double pwR = c.HP / (c.Mass / 1000.0);
        double tireCirc = Math.PI * (c.RRimD + 2.0 * c.RTW * c.RTP / 100.0 / 25.4) * 0.0254;
        bool   offRoad  = c.Dis is Dis.Rally or Dis.CC;

        // ── SPRINGS ────────────────────────────────────────────────────────────
        double hzF, hzR;
        (hzF, hzR) = c.Dis switch
        {
            Dis.Drag   => (2.0, 4.5), Dis.Drift  => (1.8, 2.2),
            Dis.Rally  => (1.5, 1.7), Dis.CC     => (1.3, 1.5),
            Dis.Touge  => (2.3, 2.2), Dis.Street => (2.1, 2.0),
            _          => (2.1, 2.2)
        };
        if (c.DT == DT.RWD) { hzR += 0.15; hzF -= 0.05; }
        if (c.DT == DT.FWD) { hzF += 0.15; hzR -= 0.05; }
        hzR += Math.Max(0, (c.HP - 300) / 300.0 * 0.25);

        double spF = 0.02012 * hzF * hzF * c.Mass * wdF;
        double spR = 0.02012 * hzR * hzR * c.Mass * wdR;

        double sMul = c.Su switch
        {
            Su.Race   => 1.10, Su.Sport => 1.00, Su.Street => 0.88,
            Su.Rally  => offRoad ? 0.85 : 0.55,
            Su.Drift  => 0.85,  _ => 0.72
        };
        spF *= sMul; spR *= sMul;
        if (c.PT == PT.Hybrid) { spF *= 1.05; spR *= 1.05; }
        spR *= PDF(c.PT, c.Asp).S;

        double SF = Cl(Math.Round(spF), 57.1, 285.6);
        double SR = Cl(Math.Round(spR), 57.1, 285.6);
        Console.WriteLine($"  Springs → F:{SF} R:{SR} kgf/mm  ({hzF:F2}/{hzR:F2} Hz, sMul={sMul:F2})");
        // road community: 57–130 kgf/mm (320–730 lb/in). Rally/CC hit min for light cars → allow 55+
        var (spMinF,spMaxF,spMinR,spMaxR) = c.Dis switch
        {
            Dis.Drag  => (55.0, 285.6, 55.0, 285.6),
            Dis.Rally => (55.0, 130.0, 55.0, 130.0),
            Dis.CC    => (55.0, 100.0, 55.0, 100.0),
            Dis.Drift => (55.0, 150.0, 55.0, 200.0),
            _         => (55.0, 200.0, 55.0, 285.6)
        };
        Check("SpringFront kgf/mm", SF, spMinF, spMaxF);
        Check("SpringRear  kgf/mm", SR, spMinR, spMaxR);
        Console.WriteLine($"           F={spF:F1} R={spR:F1} (before clamp)  lb/in F≈{SF*5.6:F0} R≈{SR*5.6:F0}");

        // ── CAMBER ─────────────────────────────────────────────────────────────
        double camF, camR;
        switch (c.Dis)
        {
            case Dis.Drag:  camF=-0.2; camR= 0.0; break;
            case Dis.Drift: camF=-4.0; camR=-1.0; break;
            case Dis.Rally: camF=-1.0; camR=-0.6; break;
            case Dis.CC:    camF=-0.5; camR=-0.5; break;
            case Dis.Touge: camF=-2.0; camR=-1.0; break;
            default:        camF=-1.5; camR=-0.8; break;
        }
        double epF2 = c.EP switch { EP.Front => -0.2, EP.Rear => 0.1, _ => 0.0 };
        double epR2 = c.EP switch { EP.Front =>  0.1, EP.Rear => -0.2, _ => 0.0 };
        double pR2  = c.Dis is Dis.Drag or Dis.CC ? 0 : -Math.Max(0,(c.HP-300)/200.0*0.15);
        camF = Cl(camF + epF2, -5, 0); camR = Cl(camR + epR2 + pR2, -5, 0);
        double CF = Math.Round(camF, 1), CR = Math.Round(camR, 1);
        Console.WriteLine($"  Camber  → F:{CF}° R:{CR}°");
        var (cFmin,cFmax,cRmin,cRmax) = c.Dis switch
        {
            Dis.Drift => (-5.0, -0.5, -3.0, 0.0),
            Dis.Drag  => (-1.0,  0.1, -0.5, 0.1),
            Dis.CC    => (-1.5,  0.0, -1.5, 0.0),
            _         => (-4.0, -0.1, -2.5, 0.0)
        };
        Check("CamberFront °", CF, cFmin, cFmax);
        Check("CamberRear  °", CR, cRmin, cRmax);

        // ── TOE ────────────────────────────────────────────────────────────────
        double toeF, toeR;
        switch (c.Dis)
        {
            case Dis.Drag:  toeF= 0.0; toeR= 0.0; break;
            case Dis.Drift: toeF=-0.5; toeR= 0.5; break;
            case Dis.Rally: toeF=-0.2; toeR= 0.1; break;
            case Dis.CC:    toeF=-0.1; toeR= 0.3; break;
            case Dis.Touge: toeF=c.DT==DT.RWD?-0.2:-0.1; toeR=c.DT==DT.FWD?0.2:0.1; break;
            default:        toeF=c.DT==DT.RWD?-0.15:-0.1; toeR=c.DT==DT.FWD?0.2:0.1; break;
        }
        double wbN = Math.Min(c.WB / 2700.0, 1.2);
        toeF /= wbN; toeR /= wbN;
        if (c.DT == DT.RWD && c.Dis != Dis.Drag)
            toeR += Math.Max(0, (c.HP - 300) / 200.0 * 0.05);
        double TF = Math.Round(Cl(toeF,-5,5),1), TR = Math.Round(Cl(toeR,-5,5),1);
        Console.WriteLine($"  Toe     → F:{TF}° R:{TR}°");
        var (tFmin,tFmax,tRmin,tRmax) = c.Dis switch
        {
            Dis.Drift => (-1.0,  0.0, 0.0, 1.0),
            Dis.Drag  => (-0.1,  0.1,-0.1, 0.1),
            _         => (-0.5,  0.1,-0.1, 0.5)
        };
        Check("ToeFront °", TF, tFmin, tFmax);
        Check("ToeRear  °", TR, tRmin, tRmax);

        // ── ARB ────────────────────────────────────────────────────────────────
        double refM = 1500.0;
        double mSc  = Math.Pow(c.Mass / refM, 0.6);
        double wdDv = (wd - 50) / 50.0;
        (double bF, double bR, string _) = (c.Dis, c.DT) switch
        {
            (Dis.Drag, _)             => (6.0, 22.0, ""),
            (Dis.Drift, DT.RWD)      => (18.0, 55.0, ""),
            (Dis.Drift, _)            => (20.0, 40.0, ""),
            (Dis.Rally, _)            => (14.0, 12.0, ""),
            (Dis.CC, _)               => (10.0, 10.0, ""),
            (Dis.Touge, DT.RWD)      => (30.0, 26.0, ""),
            (Dis.Touge, DT.FWD)      => (10.0, 32.0, ""),
            (Dis.Touge, _)            => (34.0, 28.0, ""),
            (_, DT.RWD)               => (30.0, 28.0, ""),
            (_, DT.FWD)               => (12.0, 28.0, ""),
            _                         => (28.0, 30.0, "")
        };
        double wbF2  = c.WB / 2700.0;
        double arbF2 = bF * mSc * wbF2 + wdDv * 4.0 + Math.Max(0,(c.HP-300)/200.0*3.0);
        double arbR2 = bR * mSc * wbF2 - wdDv * 4.0 + Math.Max(0,(c.HP-300)/200.0*3.0);
        double AF = Math.Round(Cl(arbF2,1,65)), AR = Math.Round(Cl(arbR2,1,65));
        Console.WriteLine($"  ARB     → F:{AF} R:{AR}");
        var (aFmin,aFmax,aRmin,aRmax) = c.Dis switch
        {
            Dis.Drag  => (1.0, 15.0, 15.0, 50.0),
            Dis.Drift => (10.0, 30.0, 35.0, 65.0),
            Dis.Rally => (5.0, 25.0, 5.0, 25.0),
            Dis.CC    => (1.0, 20.0, 1.0, 20.0),
            _         => (5.0, 55.0, 5.0, 65.0)
        };
        Check("ARBFront 1–65", AF, aFmin, aFmax);
        Check("ARBRear  1–65", AR, aRmin, aRmax);

        // ── DIFFERENTIAL ───────────────────────────────────────────────────────
        double cap = c.Di switch { Di.Stock=>.40, Di.Street=>.60, Di.Sport=>.80, _=>1.00 };
        double ac, dc;
        switch (c.Dis)
        {
            case Dis.Drag:  (ac,dc)=c.DT switch{DT.RWD=>(70,20),DT.AWD=>(85,10),_=>(80,10)}; break;
            case Dis.Drift: (ac,dc)=c.DT switch{DT.RWD=>(95,5), DT.AWD=>(80,10),_=>(0,0)};   break;
            case Dis.Rally: (ac,dc)=c.DT switch{DT.AWD=>(65,20),_=>(55,20)}; break;
            case Dis.CC:    (ac,dc)=c.DT switch{DT.AWD=>(60,25),_=>(50,25)}; break;
            default:        (ac,dc)=c.DT switch{DT.RWD=>(55,20),DT.FWD=>(85,5),_=>(90,40)}; break;
        }
        ac += Math.Max(0,(c.HP-300)/200.0*5);
        ac += (c.Mass-1400)/100.0*1.5;
        ac += c.EP switch{EP.Rear=>8,EP.Mid=>4,_=>0};
        ac -= (c.WB-2700)/500.0*3.0;
        ac *= PDF(c.PT,c.Asp).D;
        double DA=Math.Round(Cl(ac*cap,0,100)), DD=Math.Round(Cl(dc*cap,0,100));
        Console.WriteLine($"  Diff    → Accel:{DA}% Decel:{DD}%");
        var (daMin,daMax) = c.Dis switch
        { Dis.Drag=>(50.0,100.0), Dis.Drift=>(50.0,100.0), Dis.Rally=>(25.0,85.0), _=>(10.0,100.0) };
        Check("DiffAccel %", DA, daMin, daMax);
        Check("DiffDecel %", DD, 0, 50);

        // ── GEARING ────────────────────────────────────────────────────────────
        int n = Math.Max(1, Math.Min(c.Gears, 10));
        double tMs = c.MaxKmh / 3.6;
        if (n == 1)
        {
            double tot = c.MaxRPM>0&&tMs>0 ? c.MaxRPM*.95*tireCirc/(60.0*tMs) : 9.0;
            double g1  = c.Dis switch
            { Dis.Drag=>4.0, Dis.CC=>4.5, Dis.Rally=>4.0, Dis.Touge=>3.8, Dis.Drift=>3.0, _=>3.5 };
            if (pwR>200) g1+=0.3; else if (pwR<100) g1-=0.3;
            g1=Math.Max(1,g1);
            double fd1=tot/g1;
            if (fd1<2.2){fd1=2.2;g1=tot/fd1;} else if (fd1>6.1){fd1=6.1;g1=tot/fd1;}
            g1=Math.Round(g1,2); fd1=Math.Round(fd1,2);
            Console.WriteLine($"  Gearing → Gear1:{g1}  FD:{fd1}  Total:{g1*fd1:F2} (target:{tot:F2})");
            Check("Gear1",      g1,  1.5, 6.5);
            Check("FinalDrive", fd1, 2.2, 6.1);
        }
        else
        {
            double first, top;
            (first, top, string _) = c.Dis switch
            { Dis.Drift=>(3.0,.85,""), Dis.Rally=>(4.0,.70,""), Dis.CC=>(4.5,.65,""),
              Dis.Touge=>(3.8,.82,""), _=>(3.5,.78,"") };
            if (pwR>200){first+=0.3;top-=0.05;} else if (pwR<100){first-=0.3;top+=0.05;}
            double fd=c.MaxRPM>0&&tMs>0 ? c.MaxRPM*.95*tireCirc/(60.0*tMs*top) : 3.5;
            if (c.Dis!=Dis.Drag) fd*=1+Math.Max(0,(pwR-150)/200.0*.05);
            fd=Math.Round(Cl(fd,2.2,6.1),2);
            var rs=new List<double>(n);
            for(int i=0;i<n;i++) rs.Add(Math.Round(first*Math.Pow(top/first,(double)i/(n-1)),2));
            Console.WriteLine($"  Gearing → FD:{fd}  1:{rs[0]}  {n}:{rs[n-1]}");
            Check("FinalDrive",    fd,      2.2, 6.1);
            Check("Gear1",         rs[0],   2.0, 5.5);
            Check($"Gear{n}(top)", rs[n-1], 0.5, 2.0);
        }

        // ── DAMPERS ────────────────────────────────────────────────────────────
        double bReb = c.Dis switch
        { Dis.Drag=>10, Dis.Drift=>12, Dis.Rally=>9, Dis.CC=>8, Dis.Touge=>13, Dis.Street=>12, _=>14 };
        double bmpR2 = c.Dis switch
        { Dis.Drag=>.45, Dis.Drift=>.50, Dis.Rally=>.65, Dis.CC=>.67, _=>.57 };
        double mSc2  = Math.Sqrt(c.Mass/1500.0);
        double wAdj2 = (wdF-.5)*4.0;
        double rF2=bReb*mSc2+wAdj2, rR2=bReb*mSc2-wAdj2;
        if (c.DT==DT.RWD) rR2+=0.5;
        rR2+=Math.Max(0,(c.HP-300)/200.0*.5);
        double bF2=rF2*bmpR2, bR2=rR2*bmpR2;
        double sMul2=c.Su switch{Su.Race=>1.10,Su.Sport=>1.00,Su.Rally=>1.05,Su.Drift=>.95,Su.Street=>.90,_=>.85};
        rF2*=sMul2;rR2*=sMul2;bF2*=sMul2;bR2*=sMul2;
        double aD=PDF(c.PT,c.Asp).Dam; rR2*=aD; bR2*=aD;
        double RF=Math.Round(Cl(rF2,1,20)), RR=Math.Round(Cl(rR2,1,20));
        double BF=Math.Round(Cl(bF2,1,20)), BR=Math.Round(Cl(bR2,1,20));
        Console.WriteLine($"  Dampers → Reb F:{RF} R:{RR}  Bump F:{BF} R:{BR}");
        Check("ReboundFront", RF, 5, 20); Check("ReboundRear",  RR, 5, 20);
        Check("BumpFront",    BF, 3, 15); Check("BumpRear",     BR, 3, 15);

        Console.WriteLine($"\n  Running: {pass} passed, {fail} failed");
    }

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 1 — GR Supra-like: community reference (should give ~401/431 lb/in = 71.6/77 kgf/mm)
        Run(new Cfg("1. ICE RWD Road — GR Supra ref (1540kg 340HP NA 6spd)",
            1540, 340, 7000, 280, 6, 50, EP.Front, DT.RWD, Dis.Road));

        // 2 — Light sports FWD
        Run(new Cfg("2. ICE FWD Road — hot hatch (1100kg 250HP NA 6spd)",
            1100, 250, 7500, 230, 6, 65, EP.Front, DT.FWD, Dis.Road,
            WB: 2550, FRimD:17, FTW:205, FTP:45, RRimD:17, RTW:205, RTP:45));

        // 3 — Heavy AWD Rally turbo with Rally suspension (P2 fix target)
        Run(new Cfg("3. ICE AWD Rally turbo (1500kg 400HP ST 6spd, Rally susp)",
            1500, 400, 6500, 190, 6, 55, EP.Front, DT.AWD, Dis.Rally,
            Asp: Asp.SingleTurbo, Su: Su.Rally, Di: Di.Race));

        // 4 — CrossCountry AWD
        Run(new Cfg("4. ICE AWD CrossCountry (1800kg 350HP NA 6spd, Rally susp)",
            1800, 350, 5500, 160, 6, 50, EP.Front, DT.AWD, Dis.CC,
            Su: Su.Rally));

        // 5 — RWD Drift
        Run(new Cfg("5. ICE RWD Drift (1400kg 500HP ST 6spd, Sport susp)",
            1400, 500, 7200, 220, 6, 50, EP.Front, DT.RWD, Dis.Drift,
            Asp: Asp.SingleTurbo));

        // 6 — Electric AWD Road single-speed
        Run(new Cfg("6. Electric AWD Road (2100kg 700HP 18000rpm 1spd)",
            2100, 700, 18000, 250, 1, 50, EP.Front, DT.AWD, Dis.Road, PT.Electric));

        // 7 — Electric AWD Drag single-speed
        Run(new Cfg("7. Electric AWD Drag (2100kg 800HP 18000rpm 1spd)",
            2100, 800, 18000, 350, 1, 50, EP.Front, DT.AWD, Dis.Drag, PT.Electric));

        Console.WriteLine($"\n{new string('=', 70)}");
        Console.WriteLine($"  ИТОГО: {pass} passed, {fail} failed");
        Console.WriteLine(new string('=', 70));
    }
}
