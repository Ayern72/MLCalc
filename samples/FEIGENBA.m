itera=20;
antalr=50;
deltar=5/antalr;
i=1;


yrange(0,1.5);
xrange(0,5);

Pen(3);

while i<=antalr do
begin
	r=i*Deltar;
	k=1;
	n=0.2;
	while k<=itera do
	begin
		n=r*n*(1-n);
		k=k+1;
		if k > itera/3*2  then
		begin
			dot(r,n);
		end
	end
	i=i+1;		
end



	