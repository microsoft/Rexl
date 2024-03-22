// This file is included in comparison test files named "<op>.txt" for double (chained) comparison
// of the form `a op0 b op1 c`.
// It contains the values and combinations to be tested.
// It requires UDF G(a, b, c) to be defined.

::+ { xs__:s, xb__:b, xr8_:r8, xr4_:r4, xi__:i, xi4_:i4, xu4_:u4, xd__:d, xt__:t, xb_q:b?, xr8q:r8?, xr4q:r4?, xi_q:i?, xi4q:i4?, xu4q:u4?, xd_q:d?, xt_q:t? }
::+ { ys__:s, yb__:b, yr8_:r8, yr4_:r4, yi__:i, yi4_:i4, yu4_:u4, yd__:d, yt__:t, yb_q:b?, yr8q:r8?, yr4q:r4?, yi_q:i?, yi4q:i4?, yu4q:u4?, yd_q:d?, yt_q:t? }
::+ { zs__:s, zb__:b, zr8_:r8, zr4_:r4, zi__:i, zi4_:i4, zu4_:u4, zd__:d, zt__:t, zb_q:b?, zr8q:r8?, zr4q:r4?, zi_q:i?, zi4q:i4?, zu4q:u4?, zd_q:d?, zt_q:t? }

G(xs__, ys__, zs__) // RRR
G(xb__, yb__, zb__)
G(xr8_, yr8_, zr8_)
G(xr4_, yr4_, zr4_)
G(xi__, yi__, zi__)
G(xi4_, yi4_, zi4_)
G(xu4_, yu4_, zu4_)
G(xd__, yd__, zd__)
G(xt__, yt__, zt__)
G(xb_q, yb__, zb__) // QRR
G(xr8q, yr8_, zr8_)
G(xr4q, yr4_, zr4_)
G(xi_q, yi__, zi__)
G(xi4q, yi4_, zi4_)
G(xu4q, yu4_, zu4_)
G(xd_q, yd__, zd__)
G(xt_q, yt__, zt__)
G(xb__, yb_q, zb__) // RQR
G(xr8_, yr8q, zr8_)
G(xr4_, yr4q, zr4_)
G(xi__, yi_q, zi__)
G(xi4_, yi4q, zi4_)
G(xu4_, yu4q, zu4_)
G(xd__, yd_q, zd__)
G(xt__, yt_q, zt__)
G(xb__, yb__, zb_q) // RRQ
G(xr8_, yr8_, zr8q)
G(xr4_, yr4_, zr4q)
G(xi__, yi__, zi_q)
G(xi4_, yi4_, zi4q)
G(xu4_, yu4_, zu4q)
G(xd__, yd__, zd_q)
G(xt__, yt__, zt_q)
G(xb_q, yb_q, zb__) // QQR
G(xr8q, yr8q, zr8_)
G(xr4q, yr4q, zr4_)
G(xi_q, yi_q, zi__)
G(xi4q, yi4q, zi4_)
G(xu4q, yu4q, zu4_)
G(xd_q, yd_q, zd__)
G(xt_q, yt_q, zt__)
G(xb_q, yb__, zb_q) // QRQ
G(xr8q, yr8_, zr8q)
G(xr4q, yr4_, zr4q)
G(xi_q, yi__, zi_q)
G(xi4q, yi4_, zi4q)
G(xu4q, yu4_, zu4q)
G(xd_q, yd__, zd_q)
G(xt_q, yt__, zt_q)
G(xb__, yb_q, zb_q) // RQQ
G(xr8_, yr8q, zr8q)
G(xr4_, yr4q, zr4q)
G(xi__, yi_q, zi_q)
G(xi4_, yi4q, zi4q)
G(xu4_, yu4q, zu4q)
G(xd__, yd_q, zd_q)
G(xt__, yt_q, zt_q)
G(xb_q, yb_q, zb_q) // QQQ
G(xr8q, yr8q, zr8q)
G(xr4q, yr4q, zr4q)
G(xi_q, yi_q, zi_q)
G(xi4q, yi4q, zi4q)
G(xu4q, yu4q, zu4q)
G(xd_q, yd_q, zd_q)
G(xt_q, yt_q, zt_q)
