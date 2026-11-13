x=zeroes(1,30);
t=zeroes(1,30);
r=0.8;
k=2;
i=1;

%yrange(0,1);
%xrange(0,30);

%Pen(3);
t(1,1)=0.2;
x(1,1)=1;
while k<=30 do
begin
	x(1,k)=k;
	b=t(1,k-1);
	t(1,k)=r*b*(1-b);
	k=k+1;
end

plot(x,t,-)

	