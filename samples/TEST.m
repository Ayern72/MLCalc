tol=1e-3;
a=2.01;
b=2.1;

while abs(a-b)> tol do
begin
	m=(a+b)/2;
	if (1/(a-2)+1/(a-3)+1/(a-4)-10)*(1/(m-2)+1/(m-3)+1/(m-4)-10) >=0 then
	begin
		a=m;
	end
	else
	begin
		b=m;
	end
end
a