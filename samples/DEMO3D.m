% A tour of the 3D commands in four windows.
initdisplay(2, 2)

% 1. A surface z = f(x, y) with grid lines on the back sides of the box.
currentwin(1, 1)
title("fplot3d: a ripple")
xrange(-6, 6); yrange(-6, 6); zrange(-0.5, 1)
grid
zlabel("height")
fplot3d("x", "y", "sin(sqrt(x^2 + y^2) + 0.01) / (sqrt(x^2 + y^2) + 0.01)", 50)

% 2. Curves and markers from vectors and from an n×3 matrix, seen from another angle.
currentwin(1, 2)
title("plot3d: helix and points")
xrange(-1.5, 1.5); yrange(-1.5, 1.5); zrange(0, 13)
t = 0:0.05:12.6;
plot3d(cos(t), sin(t), t)
s = 0:pi/2:4*pi;
P = [cos(s); sin(s); s]';          % one point per row
plot3d(P, *)
view(20, 15)

% 3. Many single dots: the Lorenz attractor, seen by a camera placed at a fixed point.
currentwin(2, 1)
title("dot3d: Lorenz attractor")
xrange(-25, 25); yrange(-30, 30); zrange(0, 50)
pen(2)
x = 1; y = 1; z = 1; h = 0.01;
for k = 1:5000
	dx = 10 * (y - x);
	dy = x * (28 - z) - y;
	dz = x * y - 8 / 3 * z;
	x = x + h * dx; y = y + h * dy; z = z + h * dz;
	if k > 100, dot3d(x, y, z), end
end
camposition(-60, -190, 100)

% 4. Several curves at once (one per row of the matrices), each in the next pen,
%    with the camera looking at a point above the middle of the box.
currentwin(2, 2)
title("plot3d: rows of matrices")
xrange(-5, 5); yrange(-5, 5); zrange(0, 5)
t = 0:0.1:6.3;
k = (1:4)';
X = (5 - k) * cos(t);
Y = (5 - k) * sin(t);
Z = k * ones(1, length(t));
plot3d(X, Y, Z)
pen(9)
fplot3d("x", "y", "0.2 * (x^2 + y^2) / 5", 15)
camtarget(0, 0, 2)
view(-60, 35)
