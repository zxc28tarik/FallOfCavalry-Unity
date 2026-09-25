# Vertical-slice economy content

The 14B economy uses 15 real Trade Good definitions: grain, flour, raw silk, silk cloth, hides, leather, iron, timber, soap, paper, wool, weapons, armor, ammunition and horses. Historically attested category/existence claims cite Bursa silk/textile, Ottoman institutional or museum sources. Other links are period-compatible reconstruction. Every unit weight, reference value, stock, demand and recipe quantity is `SliceTuning`.

Five executable recipes connect City V2 buildings to real stock: grain→flour at a Mill; raw silk→silk cloth at a Textile Workshop; hides→leather at a Tannery; iron+timber→weapons at a Blacksmith; timber→paper at a Paper Mill. Markets for İstanbul, Bursa, Edirne and İzmit have deliberately differentiated starting stock and demand. Values are not population estimates or archival price claims.

`caravan-bursa-istanbul` is a persistent Caravan owned and managed by the fictional `mehmed-celebi-tacir`, with a real Organization Trade assignment, capacity, cash and four units of silk cloth cargo. Its accounting begins at zero and changes only through `TradeTransactionService`; no fake ledger history is authored.
