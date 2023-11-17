## Summary

The talk walks through the project showing a simple scalar algorithm, then how to apply loop unrolling, unsafe code to elide bounds checks, vectorization, unrolled vectorization, and then what is achievable by applying more advanced techniques

This allows users to see the tradeoffs between different levels of investment

The most advanced implementation is nearly 5x the performance of the simple scalar and 2x the performance of the simple vectorized implementation

## Results

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.2715/23H2/2023Update/SunValley3)
11th Gen Intel Core i9-11900H 2.50GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

| Method                  | Size   | Mean          | Error         | StdDev        | Median        | Ratio | Code Size |
|------------------------ |------- |--------------:|--------------:|--------------:|--------------:|------:|----------:|
| AddScalar               | 1      |      4.931 ns |     0.0701 ns |     0.0656 ns |      4.938 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 1      |      5.016 ns |     0.0497 ns |     0.0415 ns |      5.018 ns |  1.02 |     827 B |
| AddScalarUnrolledUnsafe | 1      |      5.311 ns |     0.1293 ns |     0.1489 ns |      5.367 ns |  1.08 |     713 B |
| AddVector128            | 1      |      4.876 ns |     0.1022 ns |     0.0906 ns |      4.913 ns |  0.99 |     581 B |
| AddVector128Unrolled    | 1      |      5.427 ns |     0.1244 ns |     0.1222 ns |      5.440 ns |  1.10 |     632 B |
| AddVectorAll            | 1      |      5.436 ns |     0.0634 ns |     0.0593 ns |      5.449 ns |  1.10 |     709 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 2      |      5.337 ns |     0.1010 ns |     0.0945 ns |      5.383 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 2      |      5.226 ns |     0.1259 ns |     0.1546 ns |      5.227 ns |  0.98 |     827 B |
| AddScalarUnrolledUnsafe | 2      |      5.595 ns |     0.1339 ns |     0.1542 ns |      5.573 ns |  1.05 |     713 B |
| AddVector128            | 2      |      5.327 ns |     0.1283 ns |     0.2473 ns |      5.314 ns |  1.01 |     581 B |
| AddVector128Unrolled    | 2      |      5.707 ns |     0.1361 ns |     0.1620 ns |      5.752 ns |  1.07 |     632 B |
| AddVectorAll            | 2      |      6.174 ns |     0.0934 ns |     0.0874 ns |      6.196 ns |  1.16 |     743 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 4      |      6.326 ns |     0.0334 ns |     0.0296 ns |      6.321 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 4      |      6.694 ns |     0.0618 ns |     0.0578 ns |      6.705 ns |  1.06 |     733 B |
| AddScalarUnrolledUnsafe | 4      |      6.282 ns |     0.1451 ns |     0.1613 ns |      6.260 ns |  0.99 |     621 B |
| AddVector128            | 4      |      5.093 ns |     0.0742 ns |     0.0657 ns |      5.104 ns |  0.81 |     542 B |
| AddVector128Unrolled    | 4      |      5.412 ns |     0.1298 ns |     0.1275 ns |      5.376 ns |  0.85 |     632 B |
| AddVectorAll            | 4      |      5.485 ns |     0.0395 ns |     0.0350 ns |      5.489 ns |  0.87 |     704 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 8      |      8.047 ns |     0.1123 ns |     0.1050 ns |      8.052 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 8      |      8.458 ns |     0.1608 ns |     0.1504 ns |      8.498 ns |  1.05 |     733 B |
| AddScalarUnrolledUnsafe | 8      |      7.818 ns |     0.1787 ns |     0.1672 ns |      7.898 ns |  0.97 |     621 B |
| AddVector128            | 8      |      5.368 ns |     0.1185 ns |     0.1051 ns |      5.369 ns |  0.67 |     542 B |
| AddVector128Unrolled    | 8      |      5.433 ns |     0.1221 ns |     0.1142 ns |      5.403 ns |  0.68 |     632 B |
| AddVectorAll            | 8      |      5.717 ns |     0.1302 ns |     0.1337 ns |      5.709 ns |  0.71 |     704 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 16     |     12.155 ns |     0.2352 ns |     0.2200 ns |     12.073 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 16     |     12.828 ns |     0.2510 ns |     0.2465 ns |     12.853 ns |  1.06 |     733 B |
| AddScalarUnrolledUnsafe | 16     |     10.378 ns |     0.2261 ns |     0.3095 ns |     10.350 ns |  0.85 |     621 B |
| AddVector128            | 16     |      6.211 ns |     0.1414 ns |     0.1737 ns |      6.252 ns |  0.51 |     542 B |
| AddVector128Unrolled    | 16     |      5.976 ns |     0.1399 ns |     0.1611 ns |      6.022 ns |  0.49 |     596 B |
| AddVectorAll            | 16     |      7.211 ns |     0.0775 ns |     0.0725 ns |      7.222 ns |  0.59 |   1,564 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 32     |     19.300 ns |     0.3969 ns |     0.3519 ns |     19.409 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 32     |     20.871 ns |     0.4269 ns |     0.4193 ns |     20.786 ns |  1.08 |     733 B |
| AddScalarUnrolledUnsafe | 32     |     15.821 ns |     0.3064 ns |     0.3528 ns |     15.933 ns |  0.82 |     621 B |
| AddVector128            | 32     |      8.559 ns |     0.1921 ns |     0.2212 ns |      8.569 ns |  0.44 |     546 B |
| AddVector128Unrolled    | 32     |      6.996 ns |     0.1594 ns |     0.2385 ns |      7.050 ns |  0.36 |     596 B |
| AddVectorAll            | 32     |      7.591 ns |     0.1338 ns |     0.1251 ns |      7.639 ns |  0.39 |   1,552 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 64     |     41.136 ns |     0.7493 ns |     0.6642 ns |     41.086 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 64     |     36.882 ns |     0.5430 ns |     0.5080 ns |     36.883 ns |  0.90 |     733 B |
| AddScalarUnrolledUnsafe | 64     |     26.911 ns |     0.5558 ns |     0.5458 ns |     26.927 ns |  0.65 |     621 B |
| AddVector128            | 64     |     12.715 ns |     0.2700 ns |     0.3511 ns |     12.794 ns |  0.31 |     546 B |
| AddVector128Unrolled    | 64     |     11.472 ns |     0.2523 ns |     0.4550 ns |     11.518 ns |  0.27 |     596 B |
| AddVectorAll            | 64     |      9.003 ns |     0.2059 ns |     0.2022 ns |      9.021 ns |  0.22 |   1,568 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 128    |     79.452 ns |     1.6039 ns |     3.4867 ns |     80.634 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 128    |     88.244 ns |     1.7315 ns |     2.0613 ns |     87.830 ns |  1.13 |     733 B |
| AddScalarUnrolledUnsafe | 128    |     58.757 ns |     1.4357 ns |     4.1652 ns |     58.747 ns |  0.76 |     621 B |
| AddVector128            | 128    |     23.329 ns |     0.1336 ns |     0.1116 ns |     23.355 ns |  0.29 |     546 B |
| AddVector128Unrolled    | 128    |     21.843 ns |     0.4601 ns |     0.9806 ns |     21.685 ns |  0.28 |     596 B |
| AddVectorAll            | 128    |     13.045 ns |     0.2811 ns |     0.4031 ns |     12.850 ns |  0.17 |   1,567 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 256    |    122.344 ns |     1.2665 ns |     0.9888 ns |    122.030 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 256    |    133.186 ns |     0.3263 ns |     0.2547 ns |    133.275 ns |  1.09 |     733 B |
| AddScalarUnrolledUnsafe | 256    |     99.732 ns |     1.8569 ns |     1.6461 ns |    100.005 ns |  0.82 |     621 B |
| AddVector128            | 256    |     44.038 ns |     0.4569 ns |     0.3815 ns |     44.079 ns |  0.36 |     546 B |
| AddVector128Unrolled    | 256    |     37.405 ns |     0.2914 ns |     0.2726 ns |     37.313 ns |  0.31 |     596 B |
| AddVectorAll            | 256    |     20.263 ns |     0.3131 ns |     0.2929 ns |     20.396 ns |  0.17 |   1,561 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 512    |    233.001 ns |     1.6822 ns |     1.4047 ns |    232.695 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 512    |    257.003 ns |     1.1118 ns |     0.9856 ns |    256.690 ns |  1.10 |     733 B |
| AddScalarUnrolledUnsafe | 512    |    199.119 ns |     3.4376 ns |     6.1103 ns |    199.315 ns |  0.87 |     621 B |
| AddVector128            | 512    |     85.066 ns |     0.3473 ns |     0.3249 ns |     84.970 ns |  0.36 |     546 B |
| AddVector128Unrolled    | 512    |     70.383 ns |     0.1009 ns |     0.0944 ns |     70.423 ns |  0.30 |     596 B |
| AddVectorAll            | 512    |     32.731 ns |     0.1840 ns |     0.1631 ns |     32.785 ns |  0.14 |   1,557 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 1024   |    452.658 ns |     4.5106 ns |     4.2192 ns |    452.462 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 1024   |    503.736 ns |     2.8515 ns |     2.3811 ns |    504.576 ns |  1.11 |     733 B |
| AddScalarUnrolledUnsafe | 1024   |    403.282 ns |     7.9133 ns |    14.2694 ns |    399.911 ns |  0.88 |     621 B |
| AddVector128            | 1024   |    195.043 ns |     3.8788 ns |     4.6174 ns |    193.703 ns |  0.43 |     546 B |
| AddVector128Unrolled    | 1024   |    146.148 ns |     2.8575 ns |     2.8064 ns |    145.788 ns |  0.32 |     596 B |
| AddVectorAll            | 1024   |     61.020 ns |     0.8817 ns |     0.8247 ns |     61.191 ns |  0.13 |   1,557 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 2048   |    929.654 ns |     9.0800 ns |     8.4934 ns |    929.845 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 2048   |  1,070.450 ns |    21.2077 ns |    19.8377 ns |  1,068.682 ns |  1.15 |     733 B |
| AddScalarUnrolledUnsafe | 2048   |    776.181 ns |    14.9875 ns |    14.7197 ns |    770.757 ns |  0.84 |     621 B |
| AddVector128            | 2048   |    361.468 ns |     3.0910 ns |     2.8913 ns |    360.397 ns |  0.39 |     546 B |
| AddVector128Unrolled    | 2048   |    281.168 ns |     1.2249 ns |     1.0229 ns |    280.976 ns |  0.30 |     596 B |
| AddVectorAll            | 2048   |    115.020 ns |     2.2656 ns |     2.2251 ns |    115.374 ns |  0.12 |   1,557 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 4096   |  1,809.915 ns |    18.2909 ns |    17.1094 ns |  1,813.118 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 4096   |  1,994.153 ns |    12.2171 ns |    10.8301 ns |  1,994.723 ns |  1.10 |     731 B |
| AddScalarUnrolledUnsafe | 4096   |  1,563.161 ns |    18.6519 ns |    17.4470 ns |  1,559.539 ns |  0.86 |     626 B |
| AddVector128            | 4096   |    720.220 ns |     4.3473 ns |     3.8538 ns |    720.038 ns |  0.40 |     544 B |
| AddVector128Unrolled    | 4096   |    565.984 ns |     2.0997 ns |     1.7533 ns |    565.331 ns |  0.31 |     596 B |
| AddVectorAll            | 4096   |    239.972 ns |     1.4069 ns |     1.3160 ns |    239.913 ns |  0.13 |   1,557 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 8192   |  3,593.112 ns |    28.4504 ns |    23.7574 ns |  3,600.345 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 8192   |  4,053.856 ns |    42.1668 ns |    39.4428 ns |  4,052.110 ns |  1.13 |     731 B |
| AddScalarUnrolledUnsafe | 8192   |  3,385.802 ns |    66.8018 ns |    59.2180 ns |  3,368.371 ns |  0.94 |     626 B |
| AddVector128            | 8192   |  1,570.623 ns |    19.0139 ns |    17.7856 ns |  1,565.155 ns |  0.44 |     544 B |
| AddVector128Unrolled    | 8192   |  1,315.443 ns |    23.8418 ns |    22.3017 ns |  1,315.538 ns |  0.37 |     596 B |
| AddVectorAll            | 8192   |    809.648 ns |    11.3653 ns |    10.6311 ns |    813.105 ns |  0.23 |   1,557 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 16384  |  7,487.430 ns |    83.4869 ns |    69.7153 ns |  7,517.749 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 16384  |  8,332.966 ns |   133.4903 ns |   118.3356 ns |  8,350.599 ns |  1.11 |     731 B |
| AddScalarUnrolledUnsafe | 16384  |  6,719.248 ns |    26.9395 ns |    23.8811 ns |  6,711.577 ns |  0.90 |     626 B |
| AddVector128            | 16384  |  3,085.308 ns |    46.6266 ns |    43.6145 ns |  3,075.274 ns |  0.41 |     544 B |
| AddVector128Unrolled    | 16384  |  2,627.060 ns |    51.8771 ns |    45.9877 ns |  2,617.678 ns |  0.35 |     594 B |
| AddVectorAll            | 16384  |  1,588.047 ns |    20.0295 ns |    18.7356 ns |  1,588.970 ns |  0.21 |   1,557 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 32768  | 14,607.710 ns |   135.1426 ns |   126.4124 ns | 14,651.495 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 32768  | 16,171.875 ns |    76.0740 ns |    71.1597 ns | 16,201.321 ns |  1.11 |     731 B |
| AddScalarUnrolledUnsafe | 32768  | 13,181.522 ns |   136.1298 ns |   127.3359 ns | 13,216.711 ns |  0.90 |     626 B |
| AddVector128            | 32768  |  5,967.190 ns |    12.6329 ns |    11.8169 ns |  5,967.021 ns |  0.41 |     544 B |
| AddVector128Unrolled    | 32768  |  5,165.122 ns |    99.4944 ns |    83.0823 ns |  5,145.827 ns |  0.35 |     594 B |
| AddVectorAll            | 32768  |  3,146.201 ns |    39.1997 ns |    36.6674 ns |  3,156.636 ns |  0.22 |   1,557 B |
|                         |        |               |               |               |               |       |           |
| AddScalar               | 65536  | 29,012.110 ns |   368.4577 ns |   326.6281 ns | 29,066.977 ns |  1.00 |     487 B |
| AddScalarUnrolled       | 65536  | 32,067.860 ns |   209.6670 ns |   196.1226 ns | 32,054.004 ns |  1.11 |     731 B |
| AddScalarUnrolledUnsafe | 65536  | 26,131.360 ns |   152.9386 ns |   127.7107 ns | 26,148.126 ns |  0.90 |     626 B |
| AddVector128            | 65536  | 11,985.951 ns |    45.1096 ns |    37.6685 ns | 11,984.048 ns |  0.41 |     544 B |
| AddVector128Unrolled    | 65536  | 10,283.107 ns |    45.1005 ns |    42.1870 ns | 10,295.789 ns |  0.35 |     594 B |
| AddVectorAll            | 65536  |  6,203.422 ns |    20.4649 ns |    19.1429 ns |  6,195.075 ns |  0.21 |   1,557 B |
