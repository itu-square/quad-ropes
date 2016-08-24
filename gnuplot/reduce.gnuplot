set term postscript
set xlabel "Number of hardware-threads"
set ylabel "Time elapsed in ns"
set output "benchmark-reduce-s1000-t16.ps"
plot "logs/benchmark-s1000.txt" every :::4::4   using 2:3:5 title "Array2D.reduce"  with errorlines,\
     "logs/benchmark-s1000.txt" every :::12::12 using 2:3:5 title "QuadRope.reduce" with errorlines
