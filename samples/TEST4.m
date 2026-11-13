initdisplay(1,1)
a=1;
while a<=4 do
begin
%	Pen(7)
	a
	tplot("x","sin(x)*cos(x/3)*cos(4*x)*sin(sqr(x))*sin(4*x)*(a*10)")
	a=a+1;
end

999