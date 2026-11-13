initdisplay(1,2)
yrange(-2,2)
fplot("X","sqrt(sin(atanh(exp(sin(X)))))");
currentwin(1,2)
yrange(-2,2)
%setpen(7)
tplot("X","sqrt(sin(atanh(exp(sin(X)))))");
