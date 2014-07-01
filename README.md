CCFrontend
==========

Author: Donald DeVun

Description: 

Frontend for CCMiner and CudaMiner. Combo Box contains a list of arguments for CCMiner or CudaMiner, one for each pool; CCMiner and CudaMiner contain separate lists.
If the primary pool (the first set of arguments on the list) fails, failover will move down the list to the next pool, continuing down the list until a successful connection is made.
Every 30 minutes, the first pool will be tried again; if it continues to fail, the failover process starts over, moving down the list.

The program displays the khash/second average over the last five minutes, the share acceptance rate, and the number of accepted share per minute average over the last five minutes.
