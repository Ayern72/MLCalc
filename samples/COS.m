k=1;
j=1
initdisplay(1,3)
antal=100;
x=ones(1,100);
y=ones(1,100);
while k<=antal do
begin
	x(1,k)=k;
	y(1,k)=sin(k);
	k=k+1;
end
while j<=3 do
begin
	currentwin(1,j)
	Pen(5+j);
	j
	xrange(0,10^j);
	yrange(-2,2);
	plot(x,y,-)
	j=j+1;
end
	