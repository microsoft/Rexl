// This file is included in comparison test files named "all.txt" for double (chained) comparison
// of the form `a op0 b op1 c`. Typically op0 and op1 are the same, but this doesn't assume that.
// It assumes that "all.inc" has previously been included.
// It contains the values and combinations to be tested.
// It requires UDF H(a, b, c) to be defined.

`` xs__ := [null, "A", "B"];
`` xb__ := [false, true];
`` xr8_ := [0/0, -3r8, 0r8, 5r8];
`` xr4_ := [CastR4(0/0), -3r4, 0r4, 5r4];
`` xi__ := [-3ia, 0ia, 5ia];
`` xi2_ := [-3i2, 0i2, 5i2];
`` xu4_ := [ 0u4, 3u4, 5u4];
`` xd__ := [Date(1900, 1, 1), Date(2023, 7, 1), Date(2023, 9, 1)];
`` xt__ := [Time(-2), Time(0), Time(3)];

`` ys__ := xs__;
`` yb__ := xb__;
`` yr8_ := xr8_;
`` yr4_ := xr4_;
`` yi__ := xi__;
`` yi2_ := xi2_;
`` yu4_ := xu4_;
`` yd__ := xd__;
`` yt__ := xt__;

`` zs__ := xs__;
`` zb__ := xb__;
`` zr8_ := xr8_;
`` zr4_ := xr4_;
`` zi__ := xi__;
`` zi2_ := xi2_;
`` zu4_ := xu4_;
`` zd__ := xd__;
`` zt__ := xt__;

`` xs_q := [null] ++ xs__;
`` xb_q := [null] ++ xb__;
`` xr8q := [null] ++ xr8_;
`` xr4q := [null] ++ xr4_;
`` xi_q := [null] ++ xi__;
`` xi2q := [null] ++ xi2_;
`` xu4q := [null] ++ xu4_;
`` xd_q := [null] ++ xd__;
`` xt_q := [null] ++ xt__;

`` ys_q := xs_q;
`` yb_q := xb_q;
`` yr8q := xr8q;
`` yr4q := xr4q;
`` yi_q := xi_q;
`` yi2q := xi2q;
`` yu4q := xu4q;
`` yd_q := xd_q;
`` yt_q := xt_q;

`` zs_q := xs_q;
`` zb_q := xb_q;
`` zr8q := xr8q;
`` zr4q := xr4q;
`` zi_q := xi_q;
`` zi2q := xi2q;
`` zu4q := xu4q;
`` zd_q := xd_q;
`` zt_q := xt_q;

`` func G(x, y, z) := Glue(x, y, z)->Wrap()
``      ->ForEach(as tup, With(a:Wrap(tup[0]), b:Wrap(tup[1]), c:Wrap(tup[2]), H(a, b, c)))->Wrap()
``      ->Polish();
`` // Only show IL for func index 3.
`` __IL_Func_Ind := 3;

G(xs__, ys__, zs__) // RRR
G(xb__, yb__, zb__)
G(xr8_, yr8_, zr8_)
G(xr4_, yr4_, zr4_)
G(xi__, yi__, zi__)
G(xi2_, yi2_, zi2_)
G(xu4_, yu4_, zu4_)
G(xd__, yd__, zd__)
G(xt__, yt__, zt__)
G(xb_q, yb__, zb__) // QRR
G(xr8q, yr8_, zr8_)
G(xr4q, yr4_, zr4_)
G(xi_q, yi__, zi__)
G(xi2q, yi2_, zi2_)
G(xu4q, yu4_, zu4_)
G(xd_q, yd__, zd__)
G(xt_q, yt__, zt__)
G(xb__, yb_q, zb__) // RQR
G(xr8_, yr8q, zr8_)
G(xr4_, yr4q, zr4_)
G(xi__, yi_q, zi__)
G(xi2_, yi2q, zi2_)
G(xu4_, yu4q, zu4_)
G(xd__, yd_q, zd__)
G(xt__, yt_q, zt__)
G(xb__, yb__, zb_q) // RRQ
G(xr8_, yr8_, zr8q)
G(xr4_, yr4_, zr4q)
G(xi__, yi__, zi_q)
G(xi2_, yi2_, zi2q)
G(xu4_, yu4_, zu4q)
G(xd__, yd__, zd_q)
G(xt__, yt__, zt_q)
G(xb_q, yb_q, zb__) // QQR
G(xr8q, yr8q, zr8_)
G(xr4q, yr4q, zr4_)
G(xi_q, yi_q, zi__)
G(xi2q, yi2q, zi2_)
G(xu4q, yu4q, zu4_)
G(xd_q, yd_q, zd__)
G(xt_q, yt_q, zt__)
G(xb_q, yb__, zb_q) // QRQ
G(xr8q, yr8_, zr8q)
G(xr4q, yr4_, zr4q)
G(xi_q, yi__, zi_q)
G(xi2q, yi2_, zi2q)
G(xu4q, yu4_, zu4q)
G(xd_q, yd__, zd_q)
G(xt_q, yt__, zt_q)
G(xb__, yb_q, zb_q) // RQQ
G(xr8_, yr8q, zr8q)
G(xr4_, yr4q, zr4q)
G(xi__, yi_q, zi_q)
G(xi2_, yi2q, zi2q)
G(xu4_, yu4q, zu4q)
G(xd__, yd_q, zd_q)
G(xt__, yt_q, zt_q)
G(xb_q, yb_q, zb_q) // QQQ
G(xr8q, yr8q, zr8q)
G(xr4q, yr4q, zr4q)
G(xi_q, yi_q, zi_q)
G(xi2q, yi2q, zi2q)
G(xu4q, yu4q, zu4q)
G(xd_q, yd_q, zd_q)
G(xt_q, yt_q, zt_q)
