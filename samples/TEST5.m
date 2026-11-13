x=[-3,-1,0,2,3,7];
y=[3.3,4.5,2,1.5,2,5.2];


p3=polyfit(x,y,4);

xrange(-4,8);
yrange(1,6);
plot(x,y,+);

polyplot(p3);

r1=p3(1,1);
r2=p3(1,2);
r3=p3(1,3);
r4=p3(1,4);
r5=p3(1,5);
pen(3)
tplot("x","r1+r2*x+r3*x*x+r4*x*x*x+r5*x*x*x*x")
fplot("x","r1+r2*x+r3*x*x+r4*x*x*x+r5*x*x*x*x")
