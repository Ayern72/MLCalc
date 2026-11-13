P=[99.3, 101.9, 104.2, 106.4, 109.1, 111.4, 113.2, 115.2, 117.5, 120.1];
T=[21,29,37,44,52,59,65,71,78,87];
res=polyfit(T,P,1);
xrange(-300,100)
yrange(-10,130);
xtitle("Temperatur")
ytitle("Tryck")
Pen(6);
polyplot(res);
