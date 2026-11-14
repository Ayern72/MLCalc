% 3D plots: a surface, a helix and a few points, from the default camera.
initdisplay(1, 2)

title("sin(x) * cos(y)")
xrange(-4, 4); yrange(-4, 4); zrange(-1.5, 1.5)
fplot3d("x", "y", "sin(x) * cos(y)")

currentwin(1, 2)
title("Helix")
xrange(-1.5, 1.5); yrange(-1.5, 1.5); zrange(0, 13)
grid
t = 0:0.1:12.6;
plot3d(cos(t), sin(t), t)
plot3d([0 0 0; 0 0 12.6], *)
