// This file is included in comparison test files named "<op>.txt" for single (not chained) comparison.
// It contains the values and combinations to be tested.
// It requires UDF G(a, b) to be defined.

::+ { xs__:s, xb__:b, xr8_:r8, xr4_:r4, xi__:i, xi4_:i4, xu4_:u4, xd__:d, xt__:t, xb_q:b?, xr8q:r8?, xr4q:r4?, xi_q:i?, xi4q:i4?, xu4q:u4?, xd_q:d?, xt_q:t? }
::+ { ys__:s, yb__:b, yr8_:r8, yr4_:r4, yi__:i, yi4_:i4, yu4_:u4, yd__:d, yt__:t, yb_q:b?, yr8q:r8?, yr4q:r4?, yi_q:i?, yi4q:i4?, yu4q:u4?, yd_q:d?, yt_q:t? }

G(xs__, ys__) // Req with Req (RR).
G(xb__, yb__)
G(xr8_, yr8_)
G(xr4_, yr4_)
G(xi__, yi__)
G(xi4_, yi4_)
G(xu4_, yu4_)
G(xd__, yd__)
G(xt__, yt__)
G(xb__, yb_q) // Req with Opt (RQ).
G(xr8_, yr8q)
G(xr4_, yr4q)
G(xi__, yi_q)
G(xi4_, yi4q)
G(xu4_, yu4q)
G(xd__, yd_q)
G(xt__, yt_q)
G(xb_q, yb__) // Opt with Req (QR).
G(xr8q, yr8_)
G(xr4q, yr4_)
G(xi_q, yi__)
G(xi4q, yi4_)
G(xu4q, yu4_)
G(xd_q, yd__)
G(xt_q, yt__)
G(xb_q, yb_q) // Opt with Opt (QQ).
G(xr8q, yr8q)
G(xr4q, yr4q)
G(xi_q, yi_q)
G(xi4q, yi4q)
G(xu4q, yu4q)
G(xd_q, yd_q)
G(xt_q, yt_q)
