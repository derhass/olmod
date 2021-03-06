set terminal png size 1920,1200

set xlabel 'real time [s]'
set ylabel 'ship position x-dimension [game units]'

set xrange [362.8:364.8]
set yrange [-11.5:-5.5]

set grid

set output 'ping20_off +34ms lag.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.020000):2 w l lw 2 t 'estimated ground truth on server', 'res_o101_p1138_sim101_olmod-0.3.6-rc3_max100_scale0_ping20_lag34.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: off +34ms lag', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping20_off +0ms lag.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.020000):2 w l lw 2 t 'estimated ground truth on server', 'res_o102_p1138_sim102_olmod-0.3.6-rc3_max100_scale0_ping20_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: off +0ms lag', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping20_weak.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.020000):2 w l lw 2 t 'estimated ground truth on server', 'res_o103_p1138_sim103_olmod-0.3.6-rc3_max100_scale33.3333_ping20_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: weak', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping20_medium.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.020000):2 w l lw 2 t 'estimated ground truth on server', 'res_o104_p1138_sim104_olmod-0.3.6-rc3_max100_scale66.6667_ping20_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: medium', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping20_strong.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.020000):2 w l lw 2 t 'estimated ground truth on server', 'res_o105_p1138_sim105_olmod-0.3.6-rc3_max100_scale100_ping20_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: strong', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping40_off +34ms lag.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.040000):2 w l lw 2 t 'estimated ground truth on server', 'res_o106_p1138_sim106_olmod-0.3.6-rc3_max100_scale0_ping40_lag34.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: off +34ms lag', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping40_off +0ms lag.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.040000):2 w l lw 2 t 'estimated ground truth on server', 'res_o107_p1138_sim107_olmod-0.3.6-rc3_max100_scale0_ping40_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: off +0ms lag', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping40_weak.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.040000):2 w l lw 2 t 'estimated ground truth on server', 'res_o108_p1138_sim108_olmod-0.3.6-rc3_max100_scale33.3333_ping40_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: weak', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping40_medium.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.040000):2 w l lw 2 t 'estimated ground truth on server', 'res_o109_p1138_sim109_olmod-0.3.6-rc3_max100_scale66.6667_ping40_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: medium', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping40_strong.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.040000):2 w l lw 2 t 'estimated ground truth on server', 'res_o110_p1138_sim110_olmod-0.3.6-rc3_max100_scale100_ping40_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: strong', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping60_off +34ms lag.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.060000):2 w l lw 2 t 'estimated ground truth on server', 'res_o111_p1138_sim111_olmod-0.3.6-rc3_max100_scale0_ping60_lag34.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: off +34ms lag', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping60_off +0ms lag.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.060000):2 w l lw 2 t 'estimated ground truth on server', 'res_o112_p1138_sim112_olmod-0.3.6-rc3_max100_scale0_ping60_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: off +0ms lag', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping60_weak.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.060000):2 w l lw 2 t 'estimated ground truth on server', 'res_o113_p1138_sim113_olmod-0.3.6-rc3_max100_scale33.3333_ping60_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: weak', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping60_medium.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.060000):2 w l lw 2 t 'estimated ground truth on server', 'res_o114_p1138_sim114_olmod-0.3.6-rc3_max100_scale66.6667_ping60_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: medium', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping60_strong.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.060000):2 w l lw 2 t 'estimated ground truth on server', 'res_o115_p1138_sim115_olmod-0.3.6-rc3_max100_scale100_ping60_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: strong', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping80_off +34ms lag.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.080000):2 w l lw 2 t 'estimated ground truth on server', 'res_o116_p1138_sim116_olmod-0.3.6-rc3_max100_scale0_ping80_lag34.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: off +34ms lag', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping80_off +0ms lag.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.080000):2 w l lw 2 t 'estimated ground truth on server', 'res_o117_p1138_sim117_olmod-0.3.6-rc3_max100_scale0_ping80_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: off +0ms lag', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping80_weak.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.080000):2 w l lw 2 t 'estimated ground truth on server', 'res_o118_p1138_sim118_olmod-0.3.6-rc3_max100_scale33.3333_ping80_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: weak', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping80_medium.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.080000):2 w l lw 2 t 'estimated ground truth on server', 'res_o119_p1138_sim119_olmod-0.3.6-rc3_max100_scale66.6667_ping80_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: medium', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping80_strong.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.080000):2 w l lw 2 t 'estimated ground truth on server', 'res_o120_p1138_sim120_olmod-0.3.6-rc3_max100_scale100_ping80_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: strong', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping100_off +34ms lag.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.100000):2 w l lw 2 t 'estimated ground truth on server', 'res_o121_p1138_sim121_olmod-0.3.6-rc3_max100_scale0_ping100_lag34.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: off +34ms lag', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping100_off +0ms lag.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.100000):2 w l lw 2 t 'estimated ground truth on server', 'res_o122_p1138_sim122_olmod-0.3.6-rc3_max100_scale0_ping100_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: off +0ms lag', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping100_weak.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.100000):2 w l lw 2 t 'estimated ground truth on server', 'res_o123_p1138_sim123_olmod-0.3.6-rc3_max100_scale33.3333_ping100_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: weak', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping100_medium.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.100000):2 w l lw 2 t 'estimated ground truth on server', 'res_o124_p1138_sim124_olmod-0.3.6-rc3_max100_scale66.6667_ping100_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: medium', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

set output 'ping100_strong.png'
plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-0.100000):2 w l lw 2 t 'estimated ground truth on server', 'res_o125_p1138_sim125_olmod-0.3.6-rc3_max100_scale100_ping100_lag0.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: strong', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' 

