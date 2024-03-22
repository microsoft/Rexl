// This file is included in comparison test files named "all.txt" for single (not chained) comparison.
// It assumes that "all.inc" has previously been included.
// It contains the values and combinations to be tested.
// It requires UDF H(a, b) to be defined.

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

`` func G(x, y) := Glue(x, y)->Wrap()
``      ->ForEach(as pair, With(a:Wrap(pair[0]), b:Wrap(pair[1]), H(a, b)))->Wrap()
``      ->Polish();
`` // Only show IL for func index 2.
`` __IL_Func_Ind := 2;

G(xs__, ys__) // Req with Req (RR).
G(xb__, yb__)
G(xr8_, yr8_)
G(xr4_, yr4_)
G(xi__, yi__)
G(xi2_, yi2_)
G(xu4_, yu4_)
G(xd__, yd__)
G(xt__, yt__)
G(xb__, yb_q) // Req with Opt (RQ).
G(xr8_, yr8q)
G(xr4_, yr4q)
G(xi__, yi_q)
G(xi2_, yi2q)
G(xu4_, yu4q)
G(xd__, yd_q)
G(xt__, yt_q)
G(xb_q, yb__) // Opt with Req (QR).
G(xr8q, yr8_)
G(xr4q, yr4_)
G(xi_q, yi__)
G(xi2q, yi2_)
G(xu4q, yu4_)
G(xd_q, yd__)
G(xt_q, yt__)
G(xb_q, yb_q) // Opt with Opt (QQ).
G(xr8q, yr8q)
G(xr4q, yr4q)
G(xi_q, yi_q)
G(xi2q, yi2q)
G(xu4q, yu4q)
G(xd_q, yd_q)
G(xt_q, yt_q)
